import { Test, TestingModule } from '@nestjs/testing';
import { ExecutionContext, ForbiddenException, UnauthorizedException } from '@nestjs/common';
import { ConsolidadoController } from '../../src/controllers/consolidado.controller';
import { RedisService } from '../../src/infrastructure/redis/redis.service';
import { JwtAuthGuard } from '../../src/auth/jwt-auth.guard';
import { AuthGuard } from '@nestjs/passport';

const makeRedis = () => ({
  getSaldo: jest.fn().mockResolvedValue({
    saldo_final: 100,
    total_creditos: 100,
    total_debitos: 0,
    updated_at: '2024-01-15T10:00:00Z',
  }),
} as unknown as RedisService);

const makeContext = (user: object | null) =>
  ({
    switchToHttp: () => ({
      getRequest: () => ({ user }),
    }),
    getHandler: () => ({}),
    getClass: () => ({}),
  }) as unknown as ExecutionContext;

describe('JwtAuthGuard — scope cashflow:read', () => {
  it('lança UnauthorizedException quando user é null (sem token)', () => {
    const guard = new JwtAuthGuard('cashflow:read');
    expect(() => guard.handleRequest(null, false)).toThrow(UnauthorizedException);
  });

  it('lança ForbiddenException quando token não tem o scope requerido', () => {
    const guard = new JwtAuthGuard('cashflow:read');
    const payload = { sub: 'svc', scope: 'cashflow:write', iss: 'test', aud: 'api', exp: 9999999999 };
    expect(() => guard.handleRequest(null, payload)).toThrow(ForbiddenException);
  });

  it('passa quando token tem o scope correto', () => {
    const guard = new JwtAuthGuard('cashflow:read');
    const payload = { sub: 'svc', scope: 'cashflow:read', iss: 'test', aud: 'api', exp: 9999999999 };
    const result = guard.handleRequest(null, payload);
    expect(result).toBe(payload);
  });

  it('passa quando token tem múltiplos scopes incluindo o requerido', () => {
    const guard = new JwtAuthGuard('cashflow:read');
    const payload = { sub: 'svc', scope: 'cashflow:read cashflow:write', iss: 'test', aud: 'api', exp: 9999999999 };
    const result = guard.handleRequest(null, payload);
    expect(result).toBe(payload);
  });

  it('propaga o erro original quando err é fornecido', () => {
    const guard = new JwtAuthGuard('cashflow:read');
    const err = new UnauthorizedException('Token expirado');
    expect(() => guard.handleRequest(err, false)).toThrow(err);
  });
});

describe('ConsolidadoController — proteção de rota', () => {
  let controller: ConsolidadoController;
  let module: TestingModule;

  beforeEach(async () => {
    module = await Test.createTestingModule({
      controllers: [ConsolidadoController],
      providers: [{ provide: RedisService, useValue: makeRedis() }],
    })
      .overrideGuard(AuthGuard('jwt'))
      .useValue({ canActivate: () => true })
      .compile();

    controller = module.get(ConsolidadoController);
  });

  afterEach(() => module.close());

  it('retorna saldo quando autenticado', async () => {
    const result = await controller.getConsolidado('2024-01-15');
    expect(result).toMatchObject({ data: '2024-01-15', saldo_final: 100 });
  });

  it('lança NotFoundException quando saldo não existe', async () => {
    const redis = module.get(RedisService);
    (redis.getSaldo as jest.Mock).mockResolvedValueOnce(null);
    await expect(controller.getConsolidado('2024-01-15')).rejects.toThrow('Consolidado não disponível');
  });
});
