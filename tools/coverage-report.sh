#!/bin/bash
# Generate a readable coverage report from Cobertura XML files
# Usage: ./tools/coverage-report.sh [coverage-dir]
#
# Output: coverage/REPORT.txt
#
# The report has two sections:
#   1. SUMMARY - High-level numbers for day-to-day health checks
#   2. GAP ANALYSIS - Detailed breakdown for improving coverage

COVERAGE_DIR="${1:-coverage}"
REPORT_FILE="$COVERAGE_DIR/REPORT.txt"

# Classes to exclude (test infrastructure, demos, generated code)
EXCLUDE_PATTERNS="TestRunner\.Framework|Program|\.Examples\.|/d__[0-9]|__c__|DisplayClass"

echo "Generating coverage report from $COVERAGE_DIR/*.xml"
echo ""

{
    echo "═══════════════════════════════════════════════════════════════"
    echo "CODE COVERAGE REPORT"
    echo "Generated: $(date -u '+%Y-%m-%d %H:%M:%S UTC')"
    echo "═══════════════════════════════════════════════════════════════"
    echo ""
    echo ""
    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
    echo "SECTION 1: SUMMARY (Day-to-Day Health Check)"
    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
    echo ""

    # Extract overall module stats from coverage element
    for xml_file in "$COVERAGE_DIR"/*.xml; do
        [ -f "$xml_file" ] || continue
        module=$(basename "$xml_file" .xml)

        # Get the main production package stats
        case "$module" in
            rulesengine)
                pkg_pattern='package name="RulesEngine"'
                ;;
            agentrouting)
                pkg_pattern='package name="AgentRouting"'
                ;;
            mafiademo)
                pkg_pattern='package name="AgentRouting.MafiaDemo"'
                ;;
            *)
                pkg_pattern="package name=\"${module^}\""
                ;;
        esac

        line_rate=$(grep -E "$pkg_pattern" "$xml_file" | head -1 | sed -E 's/.*line-rate="([^"]+)".*/\1/')
        branch_rate=$(grep -E "$pkg_pattern" "$xml_file" | head -1 | sed -E 's/.*branch-rate="([^"]+)".*/\1/')

        # Fallback: get from coverage element if package not found
        if [ -z "$line_rate" ] || [ "$line_rate" = "0" ]; then
            line_rate=$(head -5 "$xml_file" | grep -E "^<coverage" | sed -E 's/.*line-rate="([^"]+)".*/\1/')
            branch_rate=$(head -5 "$xml_file" | grep -E "^<coverage" | sed -E 's/.*branch-rate="([^"]+)".*/\1/')
        fi

        if [ -n "$line_rate" ]; then
            line_pct=$(echo "$line_rate * 100" | bc -l 2>/dev/null | xargs printf "%.1f")
            branch_pct=$(echo "$branch_rate * 100" | bc -l 2>/dev/null | xargs printf "%.1f")

            # Health indicator
            if (( $(echo "$line_rate >= 0.80" | bc -l) )); then
                health="✓ Good"
            elif (( $(echo "$line_rate >= 0.60" | bc -l) )); then
                health="○ Fair"
            else
                health="✗ Needs Work"
            fi

            printf "  %-15s  Line: %5s%%  Branch: %5s%%  %s\n" "$module" "$line_pct" "$branch_pct" "$health"
        fi
    done

    echo ""
    echo "  Legend: ✓ Good (≥80%)  ○ Fair (60-79%)  ✗ Needs Work (<60%)"
    echo ""
    echo ""
    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
    echo "SECTION 2: GAP ANALYSIS (For Improving Coverage)"
    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
    echo ""

    for xml_file in "$COVERAGE_DIR"/*.xml; do
        [ -f "$xml_file" ] || continue
        module=$(basename "$xml_file" .xml)

        # Determine which namespace prefix to filter for
        case "$module" in
            rulesengine)
                ns_filter="RulesEngine\."
                ;;
            agentrouting)
                ns_filter="AgentRouting\."
                ;;
            mafiademo)
                ns_filter="AgentRouting\.MafiaDemo\."
                ;;
            *)
                ns_filter=""
                ;;
        esac

        echo "┌─────────────────────────────────────────────────────────────"
        echo "│ MODULE: $module"
        echo "└─────────────────────────────────────────────────────────────"
        echo ""

        # Create temp file for analysis - filter to only this module's namespace
        tmpfile=$(mktemp)

        grep -E 'class name="[^"]*".*line-rate=' "$xml_file" | \
        grep -v '\.Tests' | \
        grep -vE "$EXCLUDE_PATTERNS" | \
        { [ -n "$ns_filter" ] && grep -E "class name=\"$ns_filter" || cat; } | \
        sed -E 's/.*class name="([^"]+)".*line-rate="([^"]+)".*branch-rate="([^"]+)".*/\1|\2|\3/' | \
        sort -u > "$tmpfile"

        # Function to simplify class names
        simplify_name() {
            echo "$1" | sed -E 's/<[^>]+>//g' | sed 's/`1//g' | sed 's/\/$//'
        }

        # CRITICAL: 0% coverage (should definitely have tests)
        echo "  CRITICAL (0% coverage - needs tests):"
        count=0
        declare -A seen
        while IFS='|' read -r class line branch; do
            if [ "$line" = "0" ]; then
                simple=$(simplify_name "$class")
                if [ -z "${seen[$simple]}" ]; then
                    seen[$simple]=1
                    printf "    • %s\n" "$simple"
                    ((count++))
                    [ $count -ge 10 ] && break
                fi
            fi
        done < "$tmpfile"
        [ $count -eq 0 ] && echo "    (none)"
        [ $count -ge 10 ] && echo "    ... and more (run with -v for full list)"
        echo ""

        # LOW: Under 50% coverage
        echo "  LOW (under 50% coverage):"
        count=0
        unset seen; declare -A seen
        while IFS='|' read -r class line branch; do
            line_pct=$(echo "$line * 100" | bc -l 2>/dev/null | cut -d. -f1)
            line_pct=${line_pct:-0}
            if [ "$line_pct" -gt 0 ] && [ "$line_pct" -lt 50 ] 2>/dev/null; then
                simple=$(simplify_name "$class")
                if [ -z "${seen[$simple]}" ]; then
                    seen[$simple]=1
                    printf "    • %-55s %3d%%\n" "$simple" "$line_pct"
                    ((count++))
                    [ $count -ge 10 ] && break
                fi
            fi
        done < "$tmpfile"
        [ $count -eq 0 ] && echo "    (none)"
        echo ""

        # QUICK WINS: 50-79% coverage (close to good)
        echo "  QUICK WINS (50-79% - a few more tests needed):"
        count=0
        unset seen; declare -A seen
        while IFS='|' read -r class line branch; do
            line_pct=$(echo "$line * 100" | bc -l 2>/dev/null | cut -d. -f1)
            line_pct=${line_pct:-0}
            if [ "$line_pct" -ge 50 ] && [ "$line_pct" -lt 80 ] 2>/dev/null; then
                simple=$(simplify_name "$class")
                if [ -z "${seen[$simple]}" ]; then
                    seen[$simple]=1
                    printf "    • %-55s %3d%%\n" "$simple" "$line_pct"
                    ((count++))
                    [ $count -ge 10 ] && break
                fi
            fi
        done < "$tmpfile"
        [ $count -eq 0 ] && echo "    (none)"
        echo ""

        # WELL COVERED: 100% coverage (verification)
        echo "  WELL COVERED (100% - no action needed):"
        count=0
        unset seen; declare -A seen
        while IFS='|' read -r class line branch; do
            if [ "$line" = "1" ]; then
                simple=$(simplify_name "$class")
                if [ -z "${seen[$simple]}" ]; then
                    seen[$simple]=1
                    printf "    ✓ %s\n" "$simple"
                    ((count++))
                    [ $count -ge 5 ] && break
                fi
            fi
        done < "$tmpfile"
        [ $count -eq 0 ] && echo "    (none)"
        [ $count -ge 5 ] && echo "    ... and more"
        echo ""

        rm -f "$tmpfile"
    done

    echo "═══════════════════════════════════════════════════════════════"
    echo "END OF REPORT"
    echo "═══════════════════════════════════════════════════════════════"

} > "$REPORT_FILE"

echo "Report written to: $REPORT_FILE"
echo ""
cat "$REPORT_FILE"
