/**
 * Serialize nested objects into flat [name, value] pairs suitable for hidden form inputs.
 *
 * Conventions:
 * - Numbers get "n:" prefix so the server can parse them back to numbers
 * - Booleans get "b:" prefix (only when true, value becomes "on"; false values are skipped)
 * - Arrays use bracket notation: entries[0].time, entries[0].value
 * - Nested objects use dot notation: request.profileName
 * - Strings are passed through as-is
 * - Date objects are serialized with toISOString()
 * - null and undefined values are skipped
 * - Empty strings are included
 */
export function serializeFormData(
	data: Record<string, unknown>,
	prefix?: string
): Array<[name: string, value: string]> {
	const entries: Array<[string, string]> = [];

	for (const [key, value] of Object.entries(data)) {
		const name = prefix ? `${prefix}.${key}` : key;
		appendValue(entries, name, value);
	}

	return entries;
}

function appendValue(entries: Array<[string, string]>, name: string, value: unknown): void {
	if (value === null || value === undefined) {
		return;
	}

	if (typeof value === 'string') {
		entries.push([name, value]);
		return;
	}

	if (typeof value === 'number' || typeof value === 'bigint') {
		entries.push([`n:${name}`, String(value)]);
		return;
	}

	if (typeof value === 'boolean') {
		if (value) {
			entries.push([`b:${name}`, 'on']);
		}
		// false booleans are skipped — absence means false
		return;
	}

	if (value instanceof Date) {
		entries.push([name, value.toISOString()]);
		return;
	}

	if (Array.isArray(value)) {
		for (let i = 0; i < value.length; i++) {
			const item = value[i];
			if (item !== null && item !== undefined && typeof item === 'object' && !Array.isArray(item) && !(item instanceof Date)) {
				// Array of objects: entries[0].time, entries[0].value
				for (const [subKey, subValue] of Object.entries(item as Record<string, unknown>)) {
					appendValue(entries, `${name}[${i}].${subKey}`, subValue);
				}
			} else {
				// Array of primitives: values[0], values[1]
				appendValue(entries, `${name}[${i}]`, item);
			}
		}
		return;
	}

	if (typeof value === 'object') {
		// Nested object: recurse with dot notation
		for (const [subKey, subValue] of Object.entries(value as Record<string, unknown>)) {
			appendValue(entries, `${name}.${subKey}`, subValue);
		}
		return;
	}
}
