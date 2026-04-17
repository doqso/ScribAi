import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { AuthService } from './auth.service';

export const apiKeyInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  const key = auth.apiKey();

  const cloned = key ? req.clone({ setHeaders: { 'X-API-Key': key } }) : req;

  return next(cloned).pipe(
    catchError((err) => {
      if (err.status === 401) {
        auth.clear();
        router.navigateByUrl('/login');
      }
      return throwError(() => err);
    })
  );
};
