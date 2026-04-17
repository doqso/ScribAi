import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { map, catchError, of } from 'rxjs';
import { ApiService } from './api.service';

export const adminGuard: CanActivateFn = () => {
  const api = inject(ApiService);
  const router = inject(Router);
  return api.me().pipe(
    map(me => {
      if (me.isAdmin) return true;
      router.navigateByUrl('/extractions');
      return false;
    }),
    catchError(() => {
      router.navigateByUrl('/login');
      return of(false);
    })
  );
};
