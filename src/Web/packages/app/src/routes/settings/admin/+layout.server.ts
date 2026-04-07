import { redirect } from "@sveltejs/kit";
import type { LayoutServerLoad } from "./$types";

export const load: LayoutServerLoad = async ({ locals }) => {
  if (!locals.isPlatformAdmin) {
    throw redirect(303, "/settings");
  }
  return { isPlatformAdmin: true };
};
