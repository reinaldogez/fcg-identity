# language: pt
Funcionalidade: Cadastro de usuario
  Como visitante
  Quero criar uma conta com nome, email e senha
  Para acessar a plataforma FCG

  Cenario: Cadastro bem-sucedido
    Quando eu cadastro um usuario com nome "Ana", email "ana@fcg.com" e senha "Senha@123"
    Entao recebo o status 201
    E o corpo da resposta contem o id do usuario

  Cenario: Email invalido e rejeitado
    Quando eu cadastro um usuario com nome "Ana", email "nao-e-email" e senha "Senha@123"
    Entao recebo o status 400
    E a mensagem de erro contem "e-mail"

  Cenario: Senha fraca e rejeitada
    Quando eu cadastro um usuario com nome "Ana", email "novo@fcg.com" e senha "123"
    Entao recebo o status 400
    E a mensagem de erro contem "senha"

  Cenario: Email duplicado retorna conflito
    Dado que ja existe um usuario com email "ana@fcg.com" e senha "Senha@123"
    Quando eu cadastro um usuario com nome "Ana", email "ana@fcg.com" e senha "Senha@123"
    Entao recebo o status 409
