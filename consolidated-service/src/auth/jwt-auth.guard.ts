import { Injectable, UnauthorizedException, ForbiddenException } from '@nestjs/common';
import { AuthGuard } from '@nestjs/passport';
import { JwtPayload } from './jwt.strategy';

function createScopeGuard(requiredScope: string) {
  @Injectable()
  class ScopedJwtGuard extends AuthGuard('jwt') {
    handleRequest<T = JwtPayload>(err: Error | null, user: T | false): T {
      if (err || !user) throw err ?? new UnauthorizedException();
      const payload = user as unknown as JwtPayload;
      const scopes = (payload.scope ?? '').split(' ');
      if (!scopes.includes(requiredScope)) {
        throw new ForbiddenException(`Scope '${requiredScope}' obrigatório`);
      }
      return user as T;
    }
  }
  return ScopedJwtGuard;
}

export const ReadGuard  = createScopeGuard('cashflow:read');
export const WriteGuard = createScopeGuard('cashflow:write');
