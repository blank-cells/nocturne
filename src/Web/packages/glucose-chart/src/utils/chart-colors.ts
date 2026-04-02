/**
 * Chart color utilities for resolving backend ChartColor enum values to CSS variables
 * ChartColor enum values are kebab-case strings that match CSS custom property names
 */

/**
 * Resolve a ChartColor enum value to a CSS variable reference
 * e.g. "glucose-in-range" → "var(--color-glucose-in-range)"
 */
export function resolveChartColor(color: string): string {
	return `var(--color-${color})`;
}

/**
 * Get glucose color based on mg/dL value and thresholds
 * This stays on the frontend as it's a display concern (threshold-based coloring per point)
 */
export function getGlucoseColor(
	sgvMgdl: number,
	thresholds: { low: number; high: number; veryLow: number; veryHigh: number }
): string {
	if (sgvMgdl < thresholds.veryLow) return 'var(--color-glucose-very-low)';
	if (sgvMgdl < thresholds.low) return 'var(--color-glucose-low)';
	if (sgvMgdl <= thresholds.high) return 'var(--color-glucose-in-range)';
	if (sgvMgdl <= thresholds.veryHigh) return 'var(--color-glucose-high)';
	return 'var(--color-glucose-very-high)';
}
