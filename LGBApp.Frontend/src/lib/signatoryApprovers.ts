const nameEquals = (a: string, b: string) =>
  a.localeCompare(b, undefined, { sensitivity: 'accent' }) === 0;

/** Names this signatory may act under (login name + linked account-holder names). */
export function signatoryApproverNames(
  loginName: string,
  holderNames?: string[],
): string[] {
  const names = new Set<string>();
  if (loginName.trim()) names.add(loginName.trim());
  for (const n of holderNames ?? []) {
    if (n.trim()) names.add(n.trim());
  }
  return [...names];
}

export function signatoryMatchesApproverName(
  approverName: string,
  loginName: string,
  holderNames?: string[],
): boolean {
  return signatoryApproverNames(loginName, holderNames).some((n) => nameEquals(n, approverName));
}

export function signatoryHasPendingApproval(
  pendingApprovers: string[],
  loginName: string,
  holderNames?: string[],
): boolean {
  return pendingApprovers.some((n) => signatoryMatchesApproverName(n, loginName, holderNames));
}

/** Show the login name when a pending slot is one of this user's linked holder records. */
export function formatPendingApproverName(
  approverName: string,
  loginName: string,
  holderNames?: string[],
): string {
  if (
    loginName.trim()
    && signatoryMatchesApproverName(approverName, loginName, holderNames)
    && !nameEquals(approverName, loginName)
  ) {
    return loginName.trim();
  }
  return approverName;
}

export function formatPendingApproverList(
  pendingApprovers: string[],
  loginName: string,
  holderNames?: string[],
): string {
  const seen = new Set<string>();
  const labels: string[] = [];
  for (const name of pendingApprovers) {
    const label = formatPendingApproverName(name, loginName, holderNames);
    const key = label.toLowerCase();
    if (seen.has(key)) continue;
    seen.add(key);
    labels.push(label);
  }
  return labels.join(', ');
}
