using IndexDesk.BuildingBlocks.Common.Results;

namespace IndexDesk.Modules.Portfolio.Services;

/// <summary>
/// Sharing decisions owned by the portfolio owner: visibility (private | public | link), the
/// link-restricted share token (32 random bytes base64url; only the SHA-256 hex hash is stored)
/// and the public page/clone. Restricted links fail closed as NotFound (never 403) so the
/// existence of a private portfolio is never leaked.
/// </summary>
public interface IPortfolioSharingService
{
    /// <summary>
    /// Aplica a visibilidade. Ao virar "link" gera token se não houver (o claro não é retornado
    /// aqui — use RegenerateShareLinkAsync) e ajusta a expiração quando expiresInDays informado.
    /// Voltar para "private" limpa slug, token e expiração.
    /// </summary>
    Task<Result<PortfolioVisibilityDto>> UpdateVisibilityAsync(
        Guid userId,
        Guid portfolioId,
        string visibility,
        int? expiresInDays,
        CancellationToken ct
    );

    /// <summary>Gera um novo token (invalidando o anterior) e retorna o valor CLARO uma única vez.</summary>
    Task<Result<ShareLinkDto>> RegenerateShareLinkAsync(
        Guid userId,
        Guid portfolioId,
        int expiresInDays,
        CancellationToken ct
    );

    /// <summary>Revoga o link restrito limpando token/expiração (a visibilidade permanece).</summary>
    Task<Result> RevokeShareLinkAsync(Guid userId, Guid portfolioId, CancellationToken ct);

    /// <summary>
    /// Página pública por slug. slug inexistente, carteira private, token ausente/incorreto ou
    /// link expirado → NotFound.
    /// </summary>
    Task<Result<PublicPortfolioDto>> GetPublicBySlugAsync(
        string slug,
        string? shareToken,
        CancellationToken ct
    );

    /// <summary>
    /// Clona a carteira pública para newUserId com BUYs sintéticos preservando os pesos atuais;
    /// respeita o limite de carteiras por usuário. Retorna o novo portfolioId.
    /// </summary>
    Task<Result<Guid>> ClonePublicAsync(string sourceSlug, Guid newUserId, CancellationToken ct);
}
