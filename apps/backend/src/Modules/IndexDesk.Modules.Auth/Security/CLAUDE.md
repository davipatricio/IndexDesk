# Security/ — Hashing, tokens e cookie (scoped)

> Companion to [`../CLAUDE.md`](../CLAUDE.md) (módulo Auth). Contexto geral:
> [`../../../CLAUDE.md`](../../../CLAUDE.md). **Do not modify code** when only instruction updates are requested.

## Responsabilidade

Primitivas criptográficas e de sessão do módulo: hash/verify de senha, emissão de access token
JWT, geração/hash de refresh token e gerência do cookie HttpOnly. Sem lógica de negócio de
cadastro/login — isso é `Services/AuthService`.

## Inventário

| Tipo | Papel |
| :--- | :--- |
| `IPasswordHasher` / `Argon2idPasswordHasher` | Hash Argon2id (salt 16B, hash 32B, m=65536, t=3, p=4) em formato PHC `$argon2id$v=19$...`; verify lê parâmetros do próprio hash e compara com `CryptographicOperations.FixedTimeEquals`. |
| `ITokenService` / `JwtTokenService` | Access token JWT HS256 (claims sub/email/name/jti + role + `"permission"`); refresh = 64 bytes RNG em Base64Url; `ComputeTokenHash` = SHA256 hex-lower (nunca persistir o valor cru). |
| `IAuthCookieService` / `AuthCookieService` | Cookie `refreshToken`: Path `/api/v1/auth`, HttpOnly, SameSite=Lax, Secure fora de Development; `ClearRefreshTokenCookie` expira com as mesmas opções. |

`IPasswordHasher`/`ITokenService` são singletons stateless; `IAuthCookieService` scoped — ver DI em
`AuthModuleExtensions`.

## Regras locais

- **Nunca logar** senha, token cru, hash ou conteúdo de cookie. IP/UserAgent só entram na entidade
  `RefreshTokenEntity` para auditoria.
- Não trocar Argon2id por BCrypt nem reduzir m/t/p sem decisão registrada (DEC-*).
- Fallback dev hardcoded (`DefaultSecretKey`, issuer `IndexDesk.Auth`, audience `IndexDesk.Web`) —
  produção exige `Jwt:SecretKey` real via env.
- Mudança de claims, vida útil (`Jwt:AccessTokenExpirationMinutes`, default 15) ou validação JWT
  (`ClockSkew = TimeSpan.Zero`) afeta o módulo inteiro — alterar somente no módulo + revisão.
- Qualquer primitiva nova (ex.: outro algoritmo de hash) entra aqui atrás de interface própria,
  nunca inline nos serviços/endpoints.
