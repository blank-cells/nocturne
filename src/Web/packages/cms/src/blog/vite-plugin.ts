import { readdir, readFile } from 'node:fs/promises';
import { join } from 'node:path';
import { parseFrontmatter, buildManifest } from './manifest.js';
import type { Plugin } from 'vite';

const VIRTUAL_MODULE_ID = 'virtual:blog-manifest';
const RESOLVED_ID = '\0' + VIRTUAL_MODULE_ID;

export interface BlogPluginOptions {
  contentDir: string;
}

export function blogManifest(options: BlogPluginOptions): Plugin {
  return {
    name: 'nocturne-blog-manifest',
    resolveId(id) {
      if (id === VIRTUAL_MODULE_ID) return RESOLVED_ID;
    },
    async load(id) {
      if (id !== RESOLVED_ID) return;

      const dir = options.contentDir;
      const files = await readdir(dir).catch(() => []);
      const svxFiles = files.filter((f) => f.endsWith('.svx'));

      const posts = [];
      for (const file of svxFiles) {
        const content = await readFile(join(dir, file), 'utf-8');
        const meta = parseFrontmatter(content, file);
        if (meta) posts.push(meta);
      }

      const isProduction = process.env.NODE_ENV === 'production';
      const manifest = buildManifest(posts, isProduction);

      return `export default ${JSON.stringify(manifest)};`;
    },
    handleHotUpdate({ file, server }) {
      if (file.endsWith('.svx')) {
        const mod = server.moduleGraph.getModuleById(RESOLVED_ID);
        if (mod) {
          server.moduleGraph.invalidateModule(mod);
          return [mod];
        }
      }
    },
  };
}
