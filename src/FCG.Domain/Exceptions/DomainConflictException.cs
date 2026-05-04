namespace FCG.Domain.Exceptions;

public class DomainConflictException(string mensagem) : DomainException(mensagem) { }
