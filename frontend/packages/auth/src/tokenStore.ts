import { isExpired, parseToken } from "./claims";

/**
 * Per-tab token holder. The API client reads the token at request time from here, so it
 * always sends the latest token without being recreated on every sign-in.
 */
export class TokenStore {
  private token: string | null;

  constructor(private readonly storageKey: string) {
    this.token = TokenStore.read(storageKey);
  }

  get(): string | null {
    return this.token;
  }

  set(token: string | null): void {
    this.token = token;
    try {
      if (token) sessionStorage.setItem(this.storageKey, token);
      else sessionStorage.removeItem(this.storageKey);
    } catch {
      // storage unavailable: keep the session in memory only
    }
  }

  private static read(storageKey: string): string | null {
    try {
      const token = sessionStorage.getItem(storageKey);
      const user = token ? parseToken(token) : null;
      return token && user && !isExpired(user) ? token : null;
    } catch {
      return null;
    }
  }
}
