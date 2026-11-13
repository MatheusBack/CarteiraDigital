# Catálogo de erros da API

Todos os erros seguem o formato **`application/problem+json`** ([RFC 9457](https://www.rfc-editor.org/rfc/rfc9457)):

```json
{
  "type": "https://github.com/MatheusBack/CarteiraDigital/blob/main/docs/erros.md#insufficient-funds",
  "title": "Saldo insuficiente.",
  "status": 422,
  "code": "insufficient-funds"
}
```

> **Dependa do `code`, não do `title`.** O código é estável. O texto pode mudar.

| HTTP | Significado |
|---|---|
| `400` | A requisição está malformada ou com valores inválidos. Corrija antes de tentar de novo. |
| `404` | O recurso não existe. |
| `409` | Conflito com um dado que já existe ou com outra requisição simultânea. |
| `422` | A requisição é válida, mas uma regra de negócio impede a operação. |

---

## Entrada inválida (400)

### invalid-amount
O valor precisa ser maior que zero e ter no máximo 2 casas decimais. Exemplos inválidos: `0`, `-10`, `0.001`.

### invalid-document
CPF com dígitos verificadores errados, com tamanho diferente de 11 dígitos ou com todos os dígitos iguais (`111.111.111-11`).

### invalid-holder-name
O nome do titular está vazio ou tem mais de 120 caracteres.

### idempotency-key-required
Depósitos, saques e transferências exigem o header `Idempotency-Key`, com 1 a 100 caracteres ASCII visíveis. Use um UUID novo para cada operação nova.

### invalid-cursor
O `cursor` do extrato não foi gerado pela API. Use o `nextCursor` da página anterior.

### invalid-limit
O `limit` do extrato precisa estar entre 1 e 100.

## Não encontrado (404)

### account-not-found
A conta informada não existe.

### transfer-not-found
A transferência informada não existe.

## Conflito (409)

### document-already-registered
Já existe uma conta para este CPF.

### conflict
A operação colidiu com outra feita ao mesmo tempo (ex.: duas aberturas de conta simultâneas com o mesmo CPF). Tente novamente.

## Regra de negócio (422)

### insufficient-funds
O saldo não cobre o saque ou a transferência.

### account-blocked
A conta está bloqueada: não deposita, não saca e não envia.

### destination-account-blocked
A conta de destino está bloqueada e não pode receber. Nada foi debitado da origem.

### same-account
A origem e o destino da transferência são a mesma conta.

### daily-limit-exceeded
A soma das transferências enviadas hoje passaria de R$ 5.000,00. "Hoje" é o dia de Brasília.

### deposit-limit-exceeded
O depósito passa de R$ 50.000,00 por operação.

### transfer-denied
O antifraude recusou a transferência. Ela fica registrada como `denied`, e o `transferId` vem na resposta.

### idempotency-key-reused
Esta `Idempotency-Key` já foi usada nesta conta com uma requisição diferente: outra operação ou outro corpo. Use uma chave nova.
