import type { PageServerLoad, Actions } from "./$types";
import { redirect, fail } from "@sveltejs/kit";
import { env as publicEnv } from "$env/dynamic/public";

/**
 * Three-state authorize page for the Discord bot link flow.
 *
 * - `error`: missing state token, expired, or unknown
 * - `slug-prompt`: the page was loaded on the apex (no tenant resolved).
 *   Shows a small form asking which Nocturne instance to log in to, then
 *   redirects to `https://<slug>.<baseDomain>/auth/bot/authorize?state=<token>`.
 * - `confirm`: the pending token is valid for this tenant. User either
 *   authenticates (if not already) and then clicks Connect to claim the link.
 */
export const load: PageServerLoad = async ({ url, locals }) => {
	const stateToken = url.searchParams.get("state");
	if (!stateToken) {
		return { mode: "error" as const, message: "Missing state token." };
	}

	// Detect apex: if the request host matches PUBLIC_BASE_DOMAIN exactly (no
	// subdomain), we know there's no tenant context and we need the slug-prompt
	// fallback so the user can tell us which instance they're on.
	const baseDomain = publicEnv.PUBLIC_BASE_DOMAIN;
	if (baseDomain) {
		const currentHost = url.host.toLowerCase();
		const expectedApex = baseDomain.toLowerCase();
		if (currentHost === expectedApex) {
			return {
				mode: "slug-prompt" as const,
				stateToken,
				baseDomain,
			};
		}
	}

	// Tenant subdomain: validate the pending token. If the API responds with
	// 404 or 410, the token is expired or doesn't belong to this tenant.
	try {
		const pending = await locals.apiClient.chatIdentity.getPending(stateToken);
		return {
			mode: "confirm" as const,
			stateToken,
			pendingPlatform: pending.platform ?? "",
			pendingPlatformUserId: pending.platformUserId ?? "",
			isAuthenticated: locals.isAuthenticated,
		};
	} catch (err: unknown) {
		const status =
			err && typeof err === "object" && "status" in err
				? (err as { status: number }).status
				: undefined;
		if (status === 404 || status === 410) {
			return {
				mode: "error" as const,
				message: "This link is expired or invalid. Please run /connect in your chat app again.",
			};
		}
		throw err;
	}
};

export const actions: Actions = {
	/**
	 * Claim the pending link for the current tenant. Called from the Confirm button
	 * on the authorize page after the user has authenticated.
	 */
	claim: async ({ request, locals }) => {
		if (!locals.isAuthenticated || !locals.user) {
			return fail(401, { error: "Not authenticated." });
		}

		const data = await request.formData();
		const state = data.get("state") as string | null;
		if (!state) {
			return fail(400, { error: "Missing state parameter." });
		}

		try {
			await locals.apiClient.chatIdentity.claimLink({ token: state });
		} catch (err) {
			console.error("Failed to claim chat identity link:", err);
			return fail(500, { error: "Failed to link account. Please try again." });
		}

		return { success: true };
	},

	/**
	 * Slug-prompt submit: redirects to the tenant subdomain's authorize page,
	 * preserving the state token. No API call involved; this is pure navigation.
	 */
	"pick-tenant": async ({ request }) => {
		const data = await request.formData();
		const state = data.get("state") as string | null;
		const slug = (data.get("slug") as string | null)?.trim().toLowerCase();

		if (!state) {
			return fail(400, { error: "Missing state parameter." });
		}
		if (!slug || !/^[a-z0-9][a-z0-9-]{0,62}[a-z0-9]?$/.test(slug)) {
			return fail(400, { error: "Please enter a valid instance slug (letters, digits, hyphens)." });
		}

		const baseDomain = publicEnv.PUBLIC_BASE_DOMAIN;
		if (!baseDomain) {
			return fail(500, { error: "Server misconfigured: PUBLIC_BASE_DOMAIN not set." });
		}

		throw redirect(303, `https://${slug}.${baseDomain}/auth/bot/authorize?state=${state}`);
	},
};
