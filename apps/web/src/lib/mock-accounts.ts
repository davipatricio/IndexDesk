/**
 * Local mock accounts — a placeholder seam for the real auth module.
 *
 * The .NET backend already defines `IndexDesk.Modules.Auth` (JWT + HttpOnly
 * refresh cookies). When that ships, replace this file's fixtures and the two
 * actions in `@/stores/session-store` with real calls; nothing else in the app
 * imports the account shape, so the header and `/entrar` page keep working.
 */

export type MockRole = 'investor' | 'admin';

export interface MockAccount {
  id: string;
  name: string;
  email: string;
  role: MockRole;
  initials: string;
}

export const MOCK_ACCOUNTS: MockAccount[] = [
  {
    id: 'marina-alves',
    name: 'Marina Alves',
    email: 'marina@exemplo.com',
    role: 'investor',
    initials: 'MA',
  },
  {
    id: 'rafael-costa',
    name: 'Rafael Costa',
    email: 'rafael@exemplo.com',
    role: 'investor',
    initials: 'RC',
  },
  {
    id: 'time-indexdesk',
    name: 'Time IndexDesk',
    email: 'admin@indexdesk.com',
    role: 'admin',
    initials: 'ID',
  },
];

export function findMockAccount(id: string): MockAccount | undefined {
  return MOCK_ACCOUNTS.find((account) => account.id === id);
}
