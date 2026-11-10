using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace ConsertaAi.Api.Seguranca;

/// <summary>
/// Classe com uma única responsabilidade: gerar o token JWT (padrão da aula de Autenticação JWT).
/// Em produção quem emite o token é a API Java (POST /auth/login); este serviço existe para
/// testes locais/integração quando a Java não está disponível (endpoint POST /auth/token-teste).
/// </summary>
public class TokenService
{
    private readonly JwtOptions _opcoes;

    public TokenService(JwtOptions opcoes) => _opcoes = opcoes;

    public (string Token, DateTime ExpiraEm) GerarToken(string usuarioId, string nome, string perfil)
    {
        // Claims: as informações que viajam dentro do token
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, usuarioId),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Name, nome),
            new Claim(ClaimTypes.Role, perfil)
        };

        // Chave e algoritmo usados para ASSINAR o token
        var chave = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opcoes.Chave));
        var credenciais = new SigningCredentials(chave, SecurityAlgorithms.HmacSha256);
        var expiraEm = DateTime.UtcNow.AddMinutes(_opcoes.ExpiracaoMinutos);

        var token = new JwtSecurityToken(
            issuer: _opcoes.Emissor,
            audience: _opcoes.Audiencia,
            claims: claims,
            expires: expiraEm,
            signingCredentials: credenciais);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiraEm);
    }
}
