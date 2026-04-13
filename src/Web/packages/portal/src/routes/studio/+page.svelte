<script lang="ts">
  import ContentEditor from '@nocturne/cms/editor/ContentEditor.svelte';
  import { blogMetadataFields } from '@nocturne/cms/editor/types';
  import { toSvx } from '@nocturne/cms/editor/markdown';
  import type { ContentTypeConfig, EditorCallbacks, ContentItem, ContentData } from '@nocturne/cms/editor/types';
  import type { ComponentDefinition } from '@nocturne/cms/editor/extensions/svelte-component';
  import LanguageSelector from '$lib/components/LanguageSelector.svelte';

  const portalComponents: ComponentDefinition[] = [
    {
      name: 'LanguageSelector',
      label: 'Language Selector',
      importPath: '$lib/components/LanguageSelector.svelte',
      defaultProps: { compact: 'true' },
    },
  ];

  const previewComponents: Record<string, typeof LanguageSelector> = {
    LanguageSelector,
  };

  const STORAGE_KEY = 'nocturne-studio-blog';

  function getStorage(): Record<string, ContentData> {
    try {
      return JSON.parse(localStorage.getItem(STORAGE_KEY) || '{}');
    } catch {
      return {};
    }
  }

  function setStorage(data: Record<string, ContentData>) {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(data));
  }

  /** Fetch published .svx files from the filesystem */
  async function fetchFilesystemPosts(): Promise<Array<{ slug: string; content: string; metadata: Record<string, unknown> }>> {
    try {
      const res = await fetch('/studio/content');
      if (!res.ok) return [];
      return await res.json();
    } catch {
      return [];
    }
  }

  const config: ContentTypeConfig = {
    mode: 'blog',
    label: 'Blog Posts',
    metadataFields: blogMetadataFields,
    preview: 'markdown',
  };

  const callbacks: EditorCallbacks = {
    async list(): Promise<ContentItem[]> {
      const [fsPosts, storage] = await Promise.all([
        fetchFilesystemPosts(),
        Promise.resolve(getStorage()),
      ]);

      // Filesystem posts (published, on disk)
      const fsItems: ContentItem[] = fsPosts.map((post) => ({
        id: post.slug,
        title: String(post.metadata.title || post.slug),
        status: 'published' as const,
        updatedAt: String(post.metadata.date || ''),
        metadata: post.metadata,
      }));

      // localStorage drafts that aren't already on disk
      const fsSlugs = new Set(fsPosts.map((p) => p.slug));
      const draftItems: ContentItem[] = Object.entries(storage)
        .filter(([id]) => !fsSlugs.has(id) && !fsSlugs.has(String(storage[id].metadata.slug)))
        .map(([id, data]) => ({
          id,
          title: String(data.metadata.title || 'Untitled'),
          status: 'draft' as const,
          updatedAt: String(data.metadata.date || ''),
          metadata: data.metadata,
        }));

      return [...fsItems, ...draftItems];
    },

    async load(id: string): Promise<ContentData> {
      // Check localStorage first (may have unsaved edits)
      const storage = getStorage();
      if (storage[id]) {
        return storage[id];
      }

      // Fall back to filesystem
      const fsPosts = await fetchFilesystemPosts();
      const post = fsPosts.find((p) => p.slug === id);
      if (post) {
        return { id: post.slug, content: post.content, metadata: post.metadata };
      }

      return { id, content: '', metadata: {} };
    },

    async save(id: string, content: string, metadata: Record<string, unknown>) {
      const storage = getStorage();
      storage[id] = { id, content, metadata };
      setStorage(storage);
    },

    async publish(id: string) {
      // Load from localStorage (where edits live)
      const storage = getStorage();
      const item = storage[id];
      if (!item) return;

      const slug = String(item.metadata.slug || id);
      const svxContent = toSvx(item.metadata, item.content);

      const res = await fetch('/studio/publish', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ slug, content: svxContent }),
      });

      if (!res.ok) {
        const err = await res.json().catch(() => ({ message: 'Failed to publish' }));
        throw new Error(err.message || 'Failed to publish');
      }

      // Clear from localStorage after successful publish
      delete storage[id];
      setStorage(storage);
    },

    async create(metadata: Record<string, unknown>): Promise<string> {
      const id = crypto.randomUUID();
      const storage = getStorage();
      storage[id] = { id, content: '', metadata };
      setStorage(storage);
      return id;
    },

    async delete(id: string) {
      const storage = getStorage();
      delete storage[id];
      setStorage(storage);
    },
  };
</script>

<svelte:head>
  <title>Studio - Nocturne</title>
</svelte:head>

<ContentEditor {config} {callbacks} components={portalComponents} previewComponentMap={previewComponents} />
