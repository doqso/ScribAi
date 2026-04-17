import { Injectable, signal } from '@angular/core';

const KEY = 'scribai.apiKey';

@Injectable({ providedIn: 'root' })
export class AuthService {
  readonly apiKey = signal<string | null>(localStorage.getItem(KEY));

  setKey(key: string) {
    localStorage.setItem(KEY, key);
    this.apiKey.set(key);
  }

  clear() {
    localStorage.removeItem(KEY);
    this.apiKey.set(null);
  }

  isAuthenticated(): boolean {
    return !!this.apiKey();
  }
}
