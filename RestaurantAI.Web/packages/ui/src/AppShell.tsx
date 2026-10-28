import type { ReactNode } from "react";
import { Link, NavLink } from "react-router";

export interface NavItem {
  to: string;
  label: string;
  end?: boolean;
}

export interface AppShellProps {
  appName: string;
  homePath?: string;
  nav?: NavItem[];
  user?: { label: string; detail?: string } | null;
  onSignOut?: () => void;
  children: ReactNode;
}

export function AppShell({ appName, homePath = "/", nav = [], user, onSignOut, children }: AppShellProps) {
  return (
    <>
      <header className="app-header">
        <div className="app-header-inner">
          <Link className="brand" to={homePath}>
            <span className="brand-mark" aria-hidden="true">
              RA
            </span>
            {appName}
          </Link>
          <nav className="app-nav" aria-label="Main">
            {nav.map((item) => (
              <NavLink key={item.to} to={item.to} end={item.end}>
                {item.label}
              </NavLink>
            ))}
          </nav>
          {user && (
            <div className="app-user">
              <span title={user.detail}>{user.label}</span>
              {onSignOut && (
                <button className="btn btn-ghost btn-sm" type="button" onClick={onSignOut}>
                  Sign out
                </button>
              )}
            </div>
          )}
        </div>
      </header>
      <main className="page">{children}</main>
    </>
  );
}
