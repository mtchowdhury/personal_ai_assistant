export const environment = {
  production: true,
  // Relative: the reverse proxy serving the app also proxies /api, so the
  // same bundle works from every device without a hostname baked in at build time.
  apiUrl: '/api/v1'
};
