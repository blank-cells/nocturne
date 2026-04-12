export interface BlogPostMeta {
  title: string;
  slug: string;
  date: string;
  tags: string[];
  category: string;
  author: string;
  summary: string;
  image?: string;
  draft?: boolean;
}

export interface BlogManifest {
  posts: BlogPostMeta[];
  tags: string[];
  categories: string[];
}
