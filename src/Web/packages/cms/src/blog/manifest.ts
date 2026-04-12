import type { BlogPostMeta, BlogManifest } from './types.js';

export function parseFrontmatter(content: string, filename: string): BlogPostMeta | null {
  const match = content.match(/^---\n([\s\S]*?)\n---/);
  if (!match) return null;

  const yaml = match[1];
  const meta: Record<string, unknown> = {};

  for (const line of yaml.split('\n')) {
    const colonIndex = line.indexOf(':');
    if (colonIndex === -1) continue;
    const key = line.slice(0, colonIndex).trim();
    let value: unknown = line.slice(colonIndex + 1).trim();

    // Parse arrays: [a, b, c]
    if (typeof value === 'string' && value.startsWith('[') && value.endsWith(']')) {
      value = value
        .slice(1, -1)
        .split(',')
        .map((s) => s.trim())
        .filter(Boolean);
    }
    // Parse booleans
    else if (value === 'true') value = true;
    else if (value === 'false') value = false;

    meta[key] = value;
  }

  return {
    title: String(meta.title ?? ''),
    slug: String(meta.slug ?? filename.replace(/\.svx$/, '')),
    date: String(meta.date ?? ''),
    tags: Array.isArray(meta.tags) ? meta.tags : [],
    category: String(meta.category ?? ''),
    author: String(meta.author ?? ''),
    summary: String(meta.summary ?? ''),
    image: meta.image ? String(meta.image) : undefined,
    draft: typeof meta.draft === 'boolean' ? meta.draft : undefined,
  };
}

export function buildManifest(posts: BlogPostMeta[], isProduction: boolean): BlogManifest {
  let filtered = isProduction ? posts.filter((p) => !p.draft) : posts;
  filtered = filtered.sort((a, b) => b.date.localeCompare(a.date));

  const tags = [...new Set(filtered.flatMap((p) => p.tags))].sort();
  const categories = [...new Set(filtered.map((p) => p.category))].sort();

  return { posts: filtered, tags, categories };
}
