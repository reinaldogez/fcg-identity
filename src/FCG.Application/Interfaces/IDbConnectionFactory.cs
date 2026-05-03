using System.Data;

namespace FCG.Application.Interfaces;

public interface IDbConnectionFactory
{
    IDbConnection CreateOpenConnection();
}
