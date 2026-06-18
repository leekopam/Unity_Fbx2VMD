const allowedExternalNavigationProtocols = new Set(["http:", "https:"]);

export function isAllowedExternalNavigationUrl(url) {
  try {
    const target = new URL(url);
    return allowedExternalNavigationProtocols.has(target.protocol);
  } catch {
    return false;
  }
}

export function openAllowedExternalNavigation({ shell, url }) {
  if (!isAllowedExternalNavigationUrl(url)) {
    return false;
  }

  void shell.openExternal(url);
  return true;
}
