# language: pt
Funcionalidade: Autenticacao via JWT
  Como usuario cadastrado
  Quero fazer login e receber um token
  Para acessar recursos protegidos

  Cenario: Login bem-sucedido
    Dado que existe um usuario com email "ana@fcg.com" e senha "Senha@123"
    Quando eu faco login com email "ana@fcg.com" e senha "Senha@123"
    Entao recebo o status 200
    E a resposta contem um access token e um refresh token

  Cenario: Senha incorreta retorna mensagem generica
    Dado que existe um usuario com email "ana@fcg.com" e senha "Senha@123"
    Quando eu faco login com email "ana@fcg.com" e senha "SenhaErrada@1"
    Entao recebo o status 401
    E a mensagem de erro e "Credenciais inválidas."

  Cenario: Refresh token rotaciona
    Dado que tenho um refresh token valido para "ana@fcg.com" com senha "Senha@123"
    Quando eu uso o refresh token para renovar o acesso
    Entao recebo o status 200
    E recebo um novo par de tokens
    E o refresh token anterior nao e mais aceito
