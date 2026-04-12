import { describe, it, expect } from 'vitest';
import { parseFrontmatter, buildManifest } from './manifest.js';
import type { BlogPostMeta } from './types.js';

describe('parseFrontmatter', () => {
  it('parses valid frontmatter from svx content', () => {
    const content = `---
title: Test Post
slug: test-post
date: 2026-04-12
tags: [announcement, release]
category: news
author: Rhys
summary: A test post
---

# Content here`;

    const meta = parseFrontmatter(content, 'test-post.svx');
    expect(meta).toEqual({
      title: 'Test Post',
      slug: 'test-post',
      date: '2026-04-12',
      tags: ['announcement', 'release'],
      category: 'news',
      author: 'Rhys',
      summary: 'A test post',
      image: undefined,
      draft: undefined,
    });
  });

  it('returns null for content without frontmatter', () => {
    const meta = parseFrontmatter('# Just a heading', 'no-front.svx');
    expect(meta).toBeNull();
  });

  it('handles optional image and draft fields', () => {
    const content = `---
title: Draft Post
slug: draft-post
date: 2026-04-12
tags: []
category: dev
author: Rhys
summary: A draft
image: /blog/draft.png
draft: true
---`;

    const meta = parseFrontmatter(content, 'draft-post.svx');
    expect(meta?.image).toBe('/blog/draft.png');
    expect(meta?.draft).toBe(true);
  });
});

describe('buildManifest', () => {
  it('sorts posts by date descending', () => {
    const posts = [
      makeMeta({ slug: 'old', date: '2026-01-01' }),
      makeMeta({ slug: 'new', date: '2026-04-12' }),
      makeMeta({ slug: 'mid', date: '2026-02-15' }),
    ];
    const manifest = buildManifest(posts, false);
    expect(manifest.posts.map((p) => p.slug)).toEqual(['new', 'mid', 'old']);
  });

  it('excludes drafts in production mode', () => {
    const posts = [
      makeMeta({ slug: 'published', draft: false }),
      makeMeta({ slug: 'draft', draft: true }),
    ];
    const manifest = buildManifest(posts, true);
    expect(manifest.posts).toHaveLength(1);
    expect(manifest.posts[0].slug).toBe('published');
  });

  it('includes drafts in dev mode', () => {
    const posts = [
      makeMeta({ slug: 'published', draft: false }),
      makeMeta({ slug: 'draft', draft: true }),
    ];
    const manifest = buildManifest(posts, false);
    expect(manifest.posts).toHaveLength(2);
  });

  it('collects unique tags and categories', () => {
    const posts = [
      makeMeta({ tags: ['a', 'b'], category: 'news' }),
      makeMeta({ tags: ['b', 'c'], category: 'dev' }),
    ];
    const manifest = buildManifest(posts, false);
    expect(manifest.tags).toEqual(['a', 'b', 'c']);
    expect(manifest.categories).toEqual(['dev', 'news']);
  });
});

function makeMeta(overrides: Partial<BlogPostMeta> = {}): BlogPostMeta {
  return {
    title: 'Test',
    slug: 'test',
    date: '2026-01-01',
    tags: [],
    category: 'general',
    author: 'Test',
    summary: 'Test summary',
    ...overrides,
  };
}
