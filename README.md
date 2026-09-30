# Vitrio

Plataforma para lojistas criarem lojas online (vitrines) e receberem pedidos.

- **Backend**: ASP.NET Core (.NET 10) + EF Core + PostgreSQL — `backend/BackendSystemVitrio`
- **Frontend**: Next.js 16 + React 19 — `frontend/vitriosystem`
- **Imagens**: Cloudinary (via rota `/api/upload` do Next)

## Como rodar

```bash
# 1. Banco
docker compose up -d

# 2. Backend
cd backend/BackendSystemVitrio
dotnet user-secrets init
dotnet user-secrets set "Jwt:Key" "<uma string aleatória com 64+ caracteres>"
dotnet ef migrations add orders_store_phone_indexes
dotnet ef database update
dotnet run --launch-profile https   # https://localhost:7253 e http://localhost:5020

# 3. Frontend
cd frontend/vitriosystem
cp .env.example .env.local   # preencha as chaves do Cloudinary
npm install
npm run dev                  # http://localhost:3000
```

## Rotas do frontend

| Rota | Quem acessa | O quê |
|---|---|---|
| `/` | visitante | landing page |
| `/auth/login`, `/auth/register` | visitante | entrar / criar conta de lojista |
| `/menu/*` | lojista | conta, lojas, configurações |
| `/store/[slug]` | **público** | vitrine da loja, carrinho e checkout |
| `/store/[slug]/shopkeeper` | dono da loja | painel: início, produtos, categorias, pedidos, personalização, configurações |

## API

| Método | Rota | Auth |
|---|---|---|
| POST | `/api/Auth/register` · `/login` · `/refresh` · `/logout` | — |
| GET | `/api/Auth/me` | ✔ |
| PUT | `/api/User/update-profile` · `/change-password` | ✔ |
| GET/POST | `/api/Store` | lojista |
| GET/PUT/DELETE | `/api/Store/{id}` · GET `/api/Store/{id}/dashboard` | lojista |
| GET | `/api/Product/store/{storeId}` · `/api/Category/store/{storeId}` | lojista |
| POST/PUT/DELETE | `/api/Product[/{id}]` · `/api/Category[/{id}]` | lojista |
| GET | `/api/Order/store/{storeId}?status=Pending` · `/api/Order/{id}` | lojista |
| PUT | `/api/Order/{id}/status` | lojista |
| GET | `/api/Public/stores/{slug}` · `/categories` · `/products` · `/products/{productSlug}` | — |
| POST | `/api/Public/stores/{slug}/orders` (10/min por IP) | — |

## Fluxo de pedido

1. Visitante monta o carrinho na vitrine (salvo no navegador, por loja).
2. No checkout informa nome + telefone. O backend recalcula preços do banco e **reserva o estoque** de forma atômica.
3. Lojista vê o pedido em *Pedidos*, fala com o cliente pelo WhatsApp e avança o status
   (Pendente → Confirmado → Enviado → Entregue). Cancelar devolve o estoque.
4. Pagamento é combinado direto entre loja e cliente (sem gateway, por enquanto).
