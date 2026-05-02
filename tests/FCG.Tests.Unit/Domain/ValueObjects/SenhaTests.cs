using FCG.Domain.Exceptions;
using FCG.Domain.ValueObjects;
using FluentAssertions;

namespace FCG.Tests.Unit.Domain.ValueObjects;

public class SenhaTests
{
    [Theory]
    [InlineData("Abc@1234")]
    [InlineData("Senh@F0rte!")]
    [InlineData("T3st&Segur0")]
    public void DeveCriarSenhaValida(string senhaTexto)
    {
        var senha = Senha.Validar(senhaTexto);

        senha.Texto.Should().Be(senhaTexto);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void DeveRejeitarSenhaVazia(string? senhaTexto)
    {
        Func<Senha> acao = () => Senha.Validar(senhaTexto!);

        acao.Should().Throw<DomainException>().WithMessage("*obrigatória*");
    }

    [Theory]
    [InlineData("Ab@1")]
    [InlineData("Aa@1234")]
    public void DeveRejeitarSenhaCurta(string senhaTexto)
    {
        Func<Senha> acao = () => Senha.Validar(senhaTexto);

        acao.Should().Throw<DomainException>().WithMessage("*8 caracteres*");
    }

    [Fact]
    public void DeveRejeitarSenhaSemLetra()
    {
        Func<Senha> acao = () => Senha.Validar("12345678@");

        acao.Should().Throw<DomainException>().WithMessage("*letra*");
    }

    [Fact]
    public void DeveRejeitarSenhaSemNumero()
    {
        Func<Senha> acao = () => Senha.Validar("Abcdefgh@");

        acao.Should().Throw<DomainException>().WithMessage("*número*");
    }

    [Fact]
    public void DeveRejeitarSenhaSemCaractereEspecial()
    {
        Func<Senha> acao = () => Senha.Validar("Abcdefg1");

        acao.Should().Throw<DomainException>().WithMessage("*caractere especial*");
    }
}
