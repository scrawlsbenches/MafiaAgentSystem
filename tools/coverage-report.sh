#!/bin/bash
# Generate a simple, readable coverage report from Cobertura XML files
# Usage: ./tools/coverage-report.sh [coverage-dir]
#
# This creates coverage/REPORT.txt - a simple text file that's easy for
# both humans and AI agents to read and verify.

COVERAGE_DIR="${1:-coverage}"
REPORT_FILE="$COVERAGE_DIR/REPORT.txt"

echo "Generating coverage report from $COVERAGE_DIR/*.xml"
echo ""

{
    echo "========================================"
    echo "CODE COVERAGE REPORT"
    echo "Generated: $(date -u '+%Y-%m-%d %H:%M:%S UTC')"
    echo "========================================"
    echo ""

    for xml_file in "$COVERAGE_DIR"/*.xml; do
        [ -f "$xml_file" ] || continue

        filename=$(basename "$xml_file" .xml)
        echo "----------------------------------------"
        echo "MODULE: $filename"
        echo "----------------------------------------"
        echo ""

        # Extract production code classes (exclude test classes)
        # Format: ClassName | LineRate | BranchRate
        echo "Classes with coverage < 80%:"
        echo ""

        grep -E 'class name="[^"]*".*line-rate=' "$xml_file" | \
        grep -v 'Tests\.' | \
        grep -v '/Tests/' | \
        sed -E 's/.*class name="([^"]+)".*line-rate="([^"]+)".*branch-rate="([^"]+)".*/\1|\2|\3/' | \
        while IFS='|' read -r class line branch; do
            # Convert to percentage (line-rate is 0-1)
            line_pct=$(echo "$line * 100" | bc -l 2>/dev/null | cut -d. -f1)
            line_pct=${line_pct:-0}

            if [ "$line_pct" -lt 80 ] 2>/dev/null; then
                printf "  %-60s %3d%% line, %s branch\n" "$class" "$line_pct" "$branch"
            fi
        done | sort -t'%' -k1 -n | head -20

        echo ""
        echo "Classes with 100% coverage: (sample)"
        grep -E 'class name="[^"]*".*line-rate="1"' "$xml_file" | \
        grep -v 'Tests\.' | \
        grep -v '/Tests/' | \
        sed -E 's/.*class name="([^"]+)".*/  \1/' | \
        head -10

        echo ""
    done

    echo "========================================"
    echo "END OF REPORT"
    echo "========================================"

} > "$REPORT_FILE"

echo "Report written to: $REPORT_FILE"
echo ""
cat "$REPORT_FILE"
