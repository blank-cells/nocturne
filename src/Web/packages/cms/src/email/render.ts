import type { RequestHandler } from '@sveltejs/kit';
import type { EmailComponentMap } from './component-map.js';

export interface EmailRenderOptions {
	/** Email component substitution map (strict allowlist) */
	componentMap: EmailComponentMap;
	/** Shared secret for authenticating internal requests */
	secret: string;
	/** Render function: takes template name + locale + data, returns HTML */
	renderTemplate: (
		template: string,
		locale: string,
		data: Record<string, string>,
	) => Promise<string>;
}

/**
 * Creates a SvelteKit request handler for dynamic email rendering.
 * The .NET backend POSTs to this endpoint with:
 *   { template: "weekly-summary", locale: "en", data: { userName: "Rhys", ... } }
 * and receives rendered HTML.
 */
export function createEmailRenderHandler(options: EmailRenderOptions): RequestHandler {
	const { secret, renderTemplate } = options;

	return async ({ request }) => {
		// Authenticate via shared secret
		const authHeader = request.headers.get('x-email-render-secret');
		if (authHeader !== secret) {
			return new Response('Unauthorized', { status: 401 });
		}

		const body = await request.json();
		const { template, locale, data } = body as {
			template: string;
			locale: string;
			data: Record<string, string>;
		};

		if (!template || !locale) {
			return new Response(
				JSON.stringify({ error: 'Missing required fields: template, locale' }),
				{ status: 400, headers: { 'Content-Type': 'application/json' } },
			);
		}

		try {
			const html = await renderTemplate(template, locale, data ?? {});

			return new Response(html, {
				headers: { 'Content-Type': 'text/html; charset=utf-8' },
			});
		} catch (error) {
			const message = error instanceof Error ? error.message : 'Unknown error';
			return new Response(JSON.stringify({ error: message }), {
				status: 500,
				headers: { 'Content-Type': 'application/json' },
			});
		}
	};
}
