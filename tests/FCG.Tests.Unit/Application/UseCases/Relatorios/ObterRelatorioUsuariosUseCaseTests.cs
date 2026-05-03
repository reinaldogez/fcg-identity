using FCG.Application.DTOs;
using FCG.Application.Interfaces;
using FCG.Application.UseCases.Relatorios;
using FluentAssertions;
using Moq;

namespace FCG.Tests.Unit.Application.UseCases.Relatorios;

public class ObterRelatorioUsuariosUseCaseTests
{
    private readonly Mock<IUsuarioReadRepository> _repositorioMock = new();
    private readonly ObterRelatorioUsuariosUseCase _useCase;

    public ObterRelatorioUsuariosUseCaseTests()
    {
        _useCase = new ObterRelatorioUsuariosUseCase(_repositorioMock.Object);
    }

    [Fact]
    public async Task DeveRetornarRelatorioRetornadoPeloRepositorio()
    {
        var esperado = new RelatorioUsuariosDto(
            TotalUsuarios: 100,
            TotalAtivos: 90,
            TotalInativos: 10,
            PorTipo: new TotalPorTipoDto(Usuario: 95, Administrador: 5),
            CadastrosUltimos30Dias: 15,
            CadastrosPorMes: new List<CadastroPorMesDto>
            {
                new("2026-01", 20),
                new("2026-02", 30),
            }
        );
        _repositorioMock
            .Setup(r => r.ObterRelatorioAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(esperado);

        RelatorioUsuariosDto resultado = await _useCase.ExecutarAsync();

        resultado.Should().BeSameAs(esperado);
    }

    [Fact]
    public async Task DevePropagarCancellationTokenParaRepositorio()
    {
        using CancellationTokenSource cts = new();
        _repositorioMock
            .Setup(r => r.ObterRelatorioAsync(cts.Token))
            .ReturnsAsync(
                new RelatorioUsuariosDto(0, 0, 0, new TotalPorTipoDto(0, 0), 0, [])
            );

        await _useCase.ExecutarAsync(cts.Token);

        _repositorioMock.Verify(r => r.ObterRelatorioAsync(cts.Token), Times.Once);
    }
}
