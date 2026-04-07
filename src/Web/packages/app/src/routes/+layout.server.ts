import { redirect } from "@sveltejs/kit";
import type { LayoutServerLoad } from "./$types";
import { checkOnboarding } from "$lib/server/onboarding-check";
import { PUBLIC_ROUTE_PREFIXES } from "$lib/config/public-routes";

/**
 * Root layout server load function.
 * Provides user data to all routes and enforces the onboarding gate.
 * Setup mode and site security are handled in hooks.server.ts.
 */
export const load: LayoutServerLoad = async ({ locals, cookies, url }) => {
  if (locals.isAuthenticated && locals.apiClient) {
    const isBypassed = PUBLIC_ROUTE_PREFIXES.some((prefix) =>
      url.pathname.startsWith(prefix)
    );

    if (!isBypassed) {
      const onboarding = await checkOnboarding(locals.apiClient, cookies);
      if (!onboarding.isComplete) {
        throw redirect(303, "/settings/setup");
      }
    }
  }

  return {
    user: locals.user,
    isAuthenticated: locals.isAuthenticated,
    effectivePermissions: locals.effectivePermissions ?? [],
    isPlatformAdmin: locals.isPlatformAdmin,
  };
};
