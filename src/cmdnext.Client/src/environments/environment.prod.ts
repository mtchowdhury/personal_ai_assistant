export const environment = {
  production: true,
  // Relative: nginx serves the app and proxies /api to the API container, so the
  // same bundle works from every device without a hostname baked in at build time.
  apiUrl: '/api/v1'
};
