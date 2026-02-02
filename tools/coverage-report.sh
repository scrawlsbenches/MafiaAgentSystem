#!/bin/bash
# Coverage Report Tool
# Usage: ./tools/coverage-report.sh [options]
#
# Options:
#   --summary-only    Show only the summary, no gap details
#   --module NAME     Filter to specific module (agentrouting, rulesengine, mafiademo)
#   --detail CLASS    Show method-level coverage for a specific class (partial match)
#   --help            Show this help
#
# Examples:
#   ./tools/coverage-report.sh                      # Full report with gap analysis
#   ./tools/coverage-report.sh --summary-only       # Quick status check
#   ./tools/coverage-report.sh --module agentrouting  # Focus on one module
#   ./tools/coverage-report.sh --detail BillingAgent  # See which methods need tests
#
# Output: Writes to coverage/REPORT.txt and stdout (or just stdout for --detail)

set -e

COVERAGE_DIR="coverage"
REPORT_FILE="$COVERAGE_DIR/REPORT.txt"
SUMMARY_ONLY=false
FILTER_MODULE=""
DETAIL_CLASS=""

# Parse arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        --summary-only)
            SUMMARY_ONLY=true
            shift
            ;;
        --module)
            FILTER_MODULE="$2"
            shift 2
            ;;
        --detail)
            DETAIL_CLASS="$2"
            shift 2
            ;;
        --help)
            head -17 "$0" | tail -16
            exit 0
            ;;
        *)
            echo "Unknown option: $1"
            exit 1
            ;;
    esac
done

# Exclusion patterns (test infrastructure, entry points, generated code)
EXCLUDE_PATTERNS="TestRunner\.Framework|\.Program|/Program\.|\.Examples\."

# Check if coverage directory exists
if [ ! -d "$COVERAGE_DIR" ]; then
    echo "ERROR: Coverage directory not found: $COVERAGE_DIR"
    echo ""
    echo "Run coverage first:"
    echo "  mkdir -p coverage"
    echo "  dotnet exec tools/coverage/coverlet/tools/net6.0/any/coverlet.console.dll \\"
    echo "    Tests/AgentRouting.Tests/bin/Debug/net8.0/ -t dotnet \\"
    echo "    -a 'run --project Tests/TestRunner/ --no-build -- Tests/AgentRouting.Tests/bin/Debug/net8.0/AgentRouting.Tests.dll' \\"
    echo "    -f cobertura -o coverage/agentrouting.xml"
    exit 1
fi

# Check for XML files
xml_files=("$COVERAGE_DIR"/*.xml)
if [ ! -f "${xml_files[0]}" ]; then
    echo "ERROR: No coverage XML files found in $COVERAGE_DIR/"
    echo ""
    echo "Run coverage first. See CLAUDE.md for commands."
    exit 1
fi

# Detail mode - show method-level coverage for a specific class
if [ -n "$DETAIL_CLASS" ]; then
    echo "═══════════════════════════════════════════════════════════════════════════════"
    echo "METHOD COVERAGE DETAIL"
    echo "Search: $DETAIL_CLASS"
    echo "═══════════════════════════════════════════════════════════════════════════════"
    echo ""

    found_any=false
    for xml_file in "$COVERAGE_DIR"/*.xml; do
        [ -f "$xml_file" ] || continue
        module=$(basename "$xml_file" .xml)

        # Find matching classes (case-insensitive partial match)
        while IFS= read -r class_line; do
            [ -z "$class_line" ] && continue

            # Extract class name, filename, and line-rate
            class_name=$(echo "$class_line" | sed -E 's/.*class name="([^"]+)".*/\1/')
            file_name=$(echo "$class_line" | sed -E 's/.*filename="([^"]+)".*/\1/')
            line_rate=$(echo "$class_line" | sed -E 's/.*line-rate="([^"]+)".*/\1/')
            branch_rate=$(echo "$class_line" | sed -E 's/.*branch-rate="([^"]+)".*/\1/' 2>/dev/null || echo "0")

            # Calculate percentages
            line_pct=$(echo "$line_rate * 100" | bc -l 2>/dev/null | xargs printf "%.1f" 2>/dev/null || echo "0.0")
            branch_pct=$(echo "$branch_rate * 100" | bc -l 2>/dev/null | xargs printf "%.1f" 2>/dev/null || echo "0.0")

            # Clean class name for display
            clean_class=$(echo "$class_name" | sed -E 's/<[^>]+>//g' | sed 's/`1//g')

            echo "───────────────────────────────────────────────────────────────────────────────"
            echo "CLASS: $clean_class"
            echo "Module: $module"
            echo "File: $file_name"
            echo "Coverage: ${line_pct}% line, ${branch_pct}% branch"
            echo "───────────────────────────────────────────────────────────────────────────────"
            echo ""

            found_any=true

            # Extract methods for this class
            # We need to find the class block and extract methods within it
            # Use awk to extract methods between this class and the next class or end of classes
            class_escaped=$(echo "$class_name" | sed 's/[[\.*^$()+?{|]/\\&/g')

            # Find all methods in this class
            awk -v class="$class_escaped" '
                BEGIN { in_class=0; found_class=0 }
                /<class name="[^"]*"/ {
                    if (in_class) { in_class=0 }
                    if ($0 ~ "class name=\"" class "\"") {
                        in_class=1
                        found_class=1
                    }
                }
                /<\/class>/ { if (in_class) in_class=0 }
                in_class && /<method / { print }
            ' "$xml_file" | while IFS= read -r method_line; do
                method_name=$(echo "$method_line" | sed -E 's/.*name="([^"]+)".*/\1/')
                method_sig=$(echo "$method_line" | sed -E 's/.*signature="([^"]+)".*/\1/')
                method_rate=$(echo "$method_line" | sed -E 's/.*line-rate="([^"]+)".*/\1/')

                method_pct=$(echo "$method_rate * 100" | bc -l 2>/dev/null | xargs printf "%.0f" 2>/dev/null || echo "0")

                # Status indicator
                if [ "$method_pct" -eq 100 ] 2>/dev/null; then
                    status="✓"
                elif [ "$method_pct" -eq 0 ] 2>/dev/null; then
                    status="✗"
                else
                    status="○"
                fi

                # Format signature to be more readable (remove System. prefixes, etc.)
                clean_sig=$(echo "$method_sig" | sed 's/System\.//g' | sed 's/Collections\.Generic\.//g')

                printf "  %s %3d%%  %s%s\n" "$status" "$method_pct" "$method_name" "$clean_sig"
            done

            echo ""

        done < <(grep -i "class name=\"[^\"]*${DETAIL_CLASS}[^\"]*\"" "$xml_file" 2>/dev/null | grep -v '/d__[0-9]\|__c__\|DisplayClass')
    done

    if ! $found_any; then
        echo "No classes found matching: $DETAIL_CLASS"
        echo ""
        echo "Try a broader search or check available classes with:"
        echo "  ./tools/coverage-report.sh --module agentrouting"
    fi

    echo "═══════════════════════════════════════════════════════════════════════════════"
    exit 0
fi

# Function to get human-readable time ago
time_ago() {
    local file="$1"
    local now=$(date +%s)
    local file_time=$(stat -c %Y "$file" 2>/dev/null || stat -f %m "$file" 2>/dev/null)
    local diff=$((now - file_time))

    if [ $diff -lt 60 ]; then
        echo "${diff}s ago"
    elif [ $diff -lt 3600 ]; then
        echo "$((diff / 60))m ago"
    elif [ $diff -lt 86400 ]; then
        echo "$((diff / 3600))h ago"
    else
        echo "$((diff / 86400))d ago"
    fi
}

# Function to extract coverage from XML
get_module_coverage() {
    local xml_file="$1"
    local module=$(basename "$xml_file" .xml)

    # Map module to package name
    case "$module" in
        rulesengine) pkg_name="RulesEngine" ;;
        agentrouting) pkg_name="AgentRouting" ;;
        mafiademo) pkg_name="AgentRouting.MafiaDemo" ;;
        *) pkg_name="$module" ;;
    esac

    # Try to get package-level stats
    local line_rate=$(grep -E "package name=\"$pkg_name\"" "$xml_file" 2>/dev/null | head -1 | sed -E 's/.*line-rate="([^"]+)".*/\1/')
    local branch_rate=$(grep -E "package name=\"$pkg_name\"" "$xml_file" 2>/dev/null | head -1 | sed -E 's/.*branch-rate="([^"]+)".*/\1/')

    # Fallback to coverage element
    if [ -z "$line_rate" ]; then
        line_rate=$(head -5 "$xml_file" | grep -E "^<coverage" | sed -E 's/.*line-rate="([^"]+)".*/\1/')
        branch_rate=$(head -5 "$xml_file" | grep -E "^<coverage" | sed -E 's/.*branch-rate="([^"]+)".*/\1/')
    fi

    echo "$line_rate|$branch_rate"
}

# Generate report
{
    echo "═══════════════════════════════════════════════════════════════════════════════"
    echo "COVERAGE REPORT"
    echo "Generated: $(date -u '+%Y-%m-%d %H:%M:%S UTC')"
    echo "═══════════════════════════════════════════════════════════════════════════════"
    echo ""

    # Data source section
    echo "DATA SOURCES:"
    stale_warning=false
    for xml_file in "$COVERAGE_DIR"/*.xml; do
        [ -f "$xml_file" ] || continue
        module=$(basename "$xml_file" .xml)

        # Skip if filtering
        if [ -n "$FILTER_MODULE" ] && [ "$module" != "$FILTER_MODULE" ]; then
            continue
        fi

        file_date=$(stat -c %Y "$xml_file" 2>/dev/null || stat -f %m "$xml_file" 2>/dev/null)
        age=$(time_ago "$xml_file")

        # Check if stale (more than 24 hours)
        now=$(date +%s)
        if [ $((now - file_date)) -gt 86400 ]; then
            printf "  %-25s  %s  ⚠ STALE\n" "$module.xml" "$age"
            stale_warning=true
        else
            printf "  %-25s  %s  ✓\n" "$module.xml" "$age"
        fi
    done

    if $stale_warning; then
        echo ""
        echo "  ⚠ Some data is stale. Consider re-running coverage."
    fi
    echo ""

    # Summary section
    echo "───────────────────────────────────────────────────────────────────────────────"
    echo "SUMMARY"
    echo "───────────────────────────────────────────────────────────────────────────────"
    echo ""
    printf "  %-15s  %8s  %8s  %s\n" "Module" "Line" "Branch" "Status"
    printf "  %-15s  %8s  %8s  %s\n" "------" "----" "------" "------"

    for xml_file in "$COVERAGE_DIR"/*.xml; do
        [ -f "$xml_file" ] || continue
        module=$(basename "$xml_file" .xml)

        if [ -n "$FILTER_MODULE" ] && [ "$module" != "$FILTER_MODULE" ]; then
            continue
        fi

        IFS='|' read -r line_rate branch_rate <<< "$(get_module_coverage "$xml_file")"

        if [ -n "$line_rate" ]; then
            line_pct=$(echo "$line_rate * 100" | bc -l 2>/dev/null | xargs printf "%.1f")
            branch_pct=$(echo "$branch_rate * 100" | bc -l 2>/dev/null | xargs printf "%.1f")

            if (( $(echo "$line_rate >= 0.80" | bc -l) )); then
                status="✓ Good"
            elif (( $(echo "$line_rate >= 0.60" | bc -l) )); then
                status="○ Fair"
            else
                status="✗ Needs Work"
            fi

            printf "  %-15s  %7s%%  %7s%%  %s\n" "$module" "$line_pct" "$branch_pct" "$status"
        fi
    done
    echo ""
    echo "  Target: ≥80% line coverage"
    echo ""

    # Stop here if summary only
    if $SUMMARY_ONLY; then
        echo "═══════════════════════════════════════════════════════════════════════════════"
        echo "END OF SUMMARY (use without --summary-only for full gap analysis)"
        echo "═══════════════════════════════════════════════════════════════════════════════"
        exit 0
    fi

    # Gap analysis section
    echo "═══════════════════════════════════════════════════════════════════════════════"
    echo "GAP ANALYSIS"
    echo "═══════════════════════════════════════════════════════════════════════════════"
    echo ""

    # Track exclusions for final section
    excluded_file=$(mktemp)

    for xml_file in "$COVERAGE_DIR"/*.xml; do
        [ -f "$xml_file" ] || continue
        module=$(basename "$xml_file" .xml)

        if [ -n "$FILTER_MODULE" ] && [ "$module" != "$FILTER_MODULE" ]; then
            continue
        fi

        # Map module to namespace filter
        case "$module" in
            rulesengine) ns_filter="RulesEngine\." ;;
            agentrouting) ns_filter="AgentRouting\." ;;
            mafiademo) ns_filter="AgentRouting\.MafiaDemo\." ;;
            *) ns_filter="" ;;
        esac

        # Extract all classes with coverage data and file paths
        tmpfile=$(mktemp)

        grep -E 'class name="[^"]*".*filename="[^"]*".*line-rate=' "$xml_file" | \
        { [ -n "$ns_filter" ] && grep -E "class name=\"$ns_filter" || cat; } | \
        sed -E 's/.*class name="([^"]+)".*filename="([^"]+)".*line-rate="([^"]+)".*/\1|\2|\3/' | \
        sort -u > "$tmpfile"

        # Count by category (excluding noise)
        critical=0
        low=0
        quickwin=0
        good=0

        while IFS='|' read -r class file rate; do
            # Check exclusions
            if echo "$class" | grep -qE "$EXCLUDE_PATTERNS"; then
                echo "$class|$file|$rate|$module" >> "$excluded_file"
                continue
            fi
            # Skip async state machines for counting (they're noise)
            if echo "$class" | grep -qE '/d__[0-9]+|__c__|DisplayClass'; then
                continue
            fi

            if [ "$rate" = "0" ]; then
                ((critical++)) || true
            else
                pct=$(echo "$rate * 100" | bc -l 2>/dev/null | cut -d. -f1)
                pct=${pct:-0}
                if [ "$pct" -lt 50 ] 2>/dev/null; then
                    ((low++)) || true
                elif [ "$pct" -lt 80 ] 2>/dev/null; then
                    ((quickwin++)) || true
                else
                    ((good++)) || true
                fi
            fi
        done < "$tmpfile"

        echo "───────────────────────────────────────────────────────────────────────────────"
        echo "MODULE: $module ($critical critical, $low low, $quickwin quick wins, $good good)"
        echo "───────────────────────────────────────────────────────────────────────────────"
        echo ""

        # CRITICAL (0%)
        echo "CRITICAL (0% coverage - needs tests):"
        found=0
        while IFS='|' read -r class file rate; do
            if echo "$class" | grep -qE "$EXCLUDE_PATTERNS|/d__[0-9]+|__c__|DisplayClass"; then
                continue
            fi
            if [ "$rate" = "0" ]; then
                # Clean up class name
                clean_class=$(echo "$class" | sed -E 's/<[^>]+>//g' | sed 's/`1//g')
                printf "  • %-60s\n" "$clean_class"
                printf "      → %s\n" "$file"
                ((found++)) || true
            fi
        done < "$tmpfile"
        [ $found -eq 0 ] && echo "  (none)"
        echo ""

        # LOW (<50%)
        echo "LOW (under 50% coverage):"
        found=0
        while IFS='|' read -r class file rate; do
            if echo "$class" | grep -qE "$EXCLUDE_PATTERNS|/d__[0-9]+|__c__|DisplayClass"; then
                continue
            fi
            if [ "$rate" != "0" ]; then
                pct=$(echo "$rate * 100" | bc -l 2>/dev/null | cut -d. -f1)
                pct=${pct:-0}
                if [ "$pct" -gt 0 ] && [ "$pct" -lt 50 ] 2>/dev/null; then
                    clean_class=$(echo "$class" | sed -E 's/<[^>]+>//g' | sed 's/`1//g')
                    printf "  • %-55s  %3d%%\n" "$clean_class" "$pct"
                    printf "      → %s\n" "$file"
                    ((found++)) || true
                fi
            fi
        done < "$tmpfile"
        [ $found -eq 0 ] && echo "  (none)"
        echo ""

        # QUICK WINS (50-79%)
        echo "QUICK WINS (50-79% - close to target):"
        found=0
        while IFS='|' read -r class file rate; do
            if echo "$class" | grep -qE "$EXCLUDE_PATTERNS|/d__[0-9]+|__c__|DisplayClass"; then
                continue
            fi
            pct=$(echo "$rate * 100" | bc -l 2>/dev/null | cut -d. -f1)
            pct=${pct:-0}
            if [ "$pct" -ge 50 ] && [ "$pct" -lt 80 ] 2>/dev/null; then
                clean_class=$(echo "$class" | sed -E 's/<[^>]+>//g' | sed 's/`1//g')
                printf "  • %-55s  %3d%%\n" "$clean_class" "$pct"
                printf "      → %s\n" "$file"
                ((found++)) || true
            fi
        done < "$tmpfile"
        [ $found -eq 0 ] && echo "  (none)"
        echo ""

        # WELL COVERED (≥80%)
        echo "WELL COVERED (≥80% - meets target):"
        found=0
        while IFS='|' read -r class file rate; do
            if echo "$class" | grep -qE "$EXCLUDE_PATTERNS|/d__[0-9]+|__c__|DisplayClass"; then
                continue
            fi
            pct=$(echo "$rate * 100" | bc -l 2>/dev/null | cut -d. -f1)
            pct=${pct:-100}
            if [ "$pct" -ge 80 ] 2>/dev/null; then
                clean_class=$(echo "$class" | sed -E 's/<[^>]+>//g' | sed 's/`1//g')
                printf "  ✓ %-55s  %3d%%\n" "$clean_class" "$pct"
                ((found++)) || true
            fi
        done < "$tmpfile"
        [ $found -eq 0 ] && echo "  (none)"
        echo ""

        rm -f "$tmpfile"
    done

    # Exclusions section
    echo "═══════════════════════════════════════════════════════════════════════════════"
    echo "EXCLUDED FROM ANALYSIS"
    echo "═══════════════════════════════════════════════════════════════════════════════"
    echo ""
    echo "The following were excluded (test infrastructure, entry points, examples):"
    echo "Patterns: $EXCLUDE_PATTERNS"
    echo ""

    if [ -s "$excluded_file" ]; then
        # Count by pattern
        test_infra=$(grep -c "TestRunner\.Framework" "$excluded_file" 2>/dev/null) || test_infra=0
        programs=$(grep -cE "\.Program|/Program\." "$excluded_file" 2>/dev/null) || programs=0
        examples=$(grep -c "\.Examples\." "$excluded_file" 2>/dev/null) || examples=0

        [ "$test_infra" -gt 0 ] 2>/dev/null && echo "  TestRunner.Framework.*    $test_infra classes (test infrastructure)"
        [ "$programs" -gt 0 ] 2>/dev/null && echo "  *.Program                 $programs classes (entry points)"
        [ "$examples" -gt 0 ] 2>/dev/null && echo "  *.Examples.*              $examples classes (example code)"
    else
        echo "  (none)"
    fi
    echo ""

    rm -f "$excluded_file"

    echo "═══════════════════════════════════════════════════════════════════════════════"
    echo "END OF REPORT"
    echo "═══════════════════════════════════════════════════════════════════════════════"

} | tee "$REPORT_FILE"

echo ""
echo "Report saved to: $REPORT_FILE"
