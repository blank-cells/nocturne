import { getRequestEvent, query, command } from '$app/server';
import { error } from '@sveltejs/kit';

export const getApiSecretStatus = query(async () => {
	const { locals } = getRequestEvent();
	const { apiClient } = locals;
	try {
		return await apiClient.apiSecret.getStatus();
	} catch (err: unknown) {
		if (err && typeof err === 'object' && 'status' in err && (err as { status: number }).status === 403) {
			return null;
		}
		throw error(500, 'Failed to get API secret status');
	}
});

export const regenerateApiSecret = command(async () => {
	const { locals } = getRequestEvent();
	const { apiClient } = locals;
	try {
		return await apiClient.apiSecret.regenerate();
	} catch (err) {
		console.error('Error regenerating API secret:', err);
		throw error(500, 'Failed to regenerate API secret');
	}
});
