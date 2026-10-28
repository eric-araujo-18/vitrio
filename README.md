# Vitrio

Plataforma para lojistas criarem lojas online (vitrines) e receberem pedidos.

- **Backend**: ASP.NET Core (.NET 10) + EF Core + PostgreSQL, em `backend/BackendSystemVitrio`
- **Frontend**: Next.js 16 + React 19 + Tailwind 4, em `frontend/vitriosystem`
- **Serviços**: Mercado Pago (assinaturas dos lojistas e pagamento online dos pedidos), Resend (e-mails) e Cloudinary (imagens). Opcionais: Cloudflare Turnstile (captcha no checkout) e Sentry (alerta de erros)
- **Diagramas** (classes, casos de uso e fluxos): `docs/diagramas/vitrio.drawio`

## Desenvolvimento

```bash
# 1. Banco (Postgres 17 e pgAdmin em http://localhost:5050)
docker compose up -d

# 2. Backend (em backend/BackendSystemVitrio)
dotnet user-secrets set "Jwt:Key" "<64+ caracteres aleatórios>"   # ex.: openssl rand -base64 48
dotnet run                                                        # http://localhost:5020 (Swagger em /swagger)

# 3. Frontend (em frontend/vitriosystem)
cp .env.example .env.local   # preencha as chaves do Cloudinary
npm install
npm run dev                  # http://localhost:3000
```

- A API aplica as migrations ao subir. Os valores de desenvolvimento (banco do `docker-compose`, `localhost:3000`) ficam em `appsettings.Development.json`.
- Segredos ficam nos user-secrets, nunca no `appsettings.json`. Sem eles, a parte correspondente fica desligada:
  - Mercado Pago: seção `MercadoPago`. O teste local precisa de um túnel HTTPS público (`cloudflared tunnel --url http://localhost:5020`), porque o Mercado Pago não aceita `localhost`.
  - Resend: `Email:ResendApiKey`. Sem ela, o e-mail vai para o log da API, inclusive o link de confirmação do cadastro.
  - Cloudinary: `Cloudinary:CloudName`, `Cloudinary:ApiKey` e `Cloudinary:ApiSecret`, usados na limpeza de imagens sem uso.
  - Captcha: `Turnstile:SiteKey` e `Turnstile:SecretKey`. Para testar, a Cloudflare tem chaves públicas que sempre aprovam: `1x00000000000000000000AA` e `1x0000000000000000000000000000000AA`. A secreta `2x0000000000000000000000000000000AA` sempre recusa.
- A lista de endpoints fica no Swagger.

### Testes

```bash
# Com o Postgres do docker-compose no ar (usa um banco próprio, vitrio_tests, apagado a cada execução)
dotnet test backend/BackendSystemVitrio.Tests
```

- **Unidade:** validações, regras de entrega, validador de produção, assinatura do Mercado Pago, tokens.
- **Integração:** sobem a API inteira contra o banco de testes:
  - cadastro e confirmação de e-mail;
  - pedidos com frete, estoque e cancelamento;
  - captcha;
  - Admin;
  - limpeza de cadastros;
  - `/health`, cabeçalhos e compressão.

O GitHub Actions (`.github/workflows/ci.yml`) roda esses testes e o lint e o build do frontend a cada push. Para outro banco, defina `VITRIO_TEST_DB`: o nome precisa conter "test", senão os testes se recusam a apagá-lo.

## Rotas do frontend

| Rota | Quem acessa | O quê |
|---|---|---|
| `/` | visitante | página inicial |
| `/auth/login`, `/auth/register` | visitante | entrar e criar conta de lojista |
| `/auth/confirm-email` | lojista | link de confirmação do e-mail (sem ele, não cria loja nem assina plano) |
| `/menu/*` | lojista | lojas, assinatura e perfil |
| `/menu/admin` | Admin | números da plataforma, busca de contas, confirmar e-mail e trocar plano sem cobrança |
| `/store/[slug]` | **público** | link da vitrine (abre `/store/[slug]/client`): carrinho, checkout, conta do cliente |
| `/store/[slug]/shopkeeper` | dono da loja | painel: início, produtos, categorias, pedidos, personalização e configurações |

## Fluxo de pedido

1. O visitante monta o carrinho na vitrine (salvo no navegador, por loja).
2. No checkout ele escolhe **pagar agora** ou **combinar com a loja**.
   - Pagar agora usa o Mercado Pago, na conta da loja, e só aparece se a loja conectou a conta e o plano permite.
   - Nos dois casos, o backend recalcula os preços e reserva o estoque.
   - A entrega segue a regra da loja (Configurações da loja):
     - **a combinar**: fica fora do total, inclusive no pagamento online;
     - **grátis**;
     - **taxa fixa**: com frete grátis opcional a partir de um valor.
   - Com o captcha configurado, o pedido só sai depois da verificação da Cloudflare.
   - Cada telefone pode ter no máximo 3 pedidos esperando pagamento na mesma loja.
3. Um pedido pago online espera o pagamento (até 60 minutos) e só chega ao lojista depois de aprovado. Um pedido para combinar chega na hora.
4. O lojista é avisado no painel e por e-mail, e avança o status: Pendente → Confirmado → Enviado → Entregue. Cancelar devolve o estoque e, se o pedido foi pago online, estorna o valor.

## Produção

### 1. Domínio

Coloque o frontend e a API no **mesmo domínio**, os dois com HTTPS. Por exemplo, `https://vitrio.com.br` para o frontend e `https://api.vitrio.com.br` para a API.

O login usa um cookie da API. Com os dois em domínios diferentes (por exemplo, `*.vercel.app` e `*.onrender.com`), Safari e Chrome bloqueiam esse cookie, e o usuário sai da conta toda vez que recarrega a página.

### 2. Backend

Em produção (`ASPNETCORE_ENVIRONMENT` diferente de `Development`), os valores vêm de variáveis de ambiente, com `__` no lugar de `:`.

**A API não sobe se faltar alguma variável obrigatória ou se alguma estiver errada.** Nesse caso, o erro lista todas as que precisam de correção.

| Variável | Valor |
|---|---|
| `ConnectionStrings__DefaultConnection` | conexão do Postgres de produção |
| `Jwt__Key` | 64+ caracteres aleatórios (`openssl rand -base64 48`), diferente da de desenvolvimento |
| `App__FrontendUrl` | `https://vitrio.com.br` (vai nos links de e-mail e nas voltas do Mercado Pago) |
| `Cors__AllowedOrigins__0` | `https://vitrio.com.br` (mais origens: `__1`, `__2`...) |
| `MercadoPago__AccessToken` | access token **de produção** do app das assinaturas |
| `MercadoPago__WebhookSecret` | assinatura secreta do webhook desse app |
| `MercadoPago__BackUrl` | `https://vitrio.com.br/menu/subscription` |
| `Email__ResendApiKey` | chave do Resend |
| `Email__From` | `Vitrio <nao-responda@vitrio.com.br>`, de um domínio verificado no Resend |

`MercadoPago__TestPayerEmail` e `MercadoPago__TestOrderPayerEmail` precisam ficar **vazios**. Eles servem só para o sandbox.

**Opcionais.** Quem configura parte de um grupo precisa configurar o grupo inteiro, senão a API acusa.

| Variável | Para quê |
|---|---|
| `MercadoPago__ClientId`, `MercadoPago__ClientSecret` | app do marketplace (pagamento online dos pedidos), credenciais de produção |
| `MercadoPago__TokenEncryptionKey` | 32 bytes em base64 (`openssl rand -base64 32`); se mudar, as lojas precisam conectar de novo |
| `MercadoPago__PublicApiUrl` | `https://api.vitrio.com.br` |
| `MercadoPago__MarketplaceFeePercent` | comissão do Vitrio por venda online, em % (padrão 0) |
| `Cloudinary__CloudName`, `Cloudinary__ApiKey`, `Cloudinary__ApiSecret` | limpeza de imagens sem uso |
| `Cloudinary__Folder` | pasta das imagens, igual à do frontend; use uma por ambiente (ex.: `vitrio-prod`) |
| `Cloudinary__CleanupMode` | `DryRun` (padrão: só registra no log o que apagaria) ou `Delete`, depois de conferir o log |
| `Turnstile__SiteKey`, `Turnstile__SecretKey` | captcha da Cloudflare no checkout, contra pedidos falsos que seguram o estoque. Grátis: crie um widget em Cloudflare > Turnstile com o domínio do site. A vitrine recebe a chave pública pela API, então o frontend não precisa de configuração |
| `Sentry__Dsn` | manda os erros da API para o Sentry, que avisa por e-mail (plano gratuito). Sem ela, os erros ficam só no log da hospedagem |

- `Jwt__Key` nunca pode ser uma das chaves que já ficaram no histórico do git: o repositório é público, e a API recusa essas chaves.
- As migrations são aplicadas ao subir (`Database__ApplyMigrationsOnStartup`, padrão `true`). Para isso, o usuário do banco precisa poder criar tabelas.
- Rode **uma instância só** da API. As tarefas automáticas, o limite de tentativas e a fila de e-mails ficam na memória de cada instância.
- Configure backup automático do Postgres.

### 3. Proxy da hospedagem

A API limita tentativas por IP (login, cadastro e pedidos). Atrás do proxy da hospedagem, ela precisa ler o IP real no `X-Forwarded-For`. Sem isso, todos os usuários dividem o mesmo limite.

Ela só aceita esse cabeçalho de proxies conhecidos. Configure conforme a hospedagem:

| Variável | Quando usar |
|---|---|
| `ReverseProxy__TrustAll=true` | a API só pode ser acessada passando pelo proxy da hospedagem (Render, Railway, Fly.io, Azure App Service...) |
| `ReverseProxy__KnownNetworks__0` | faixa de IPs do proxy em CIDR, ex.: `10.0.0.0/8` |
| `ReverseProxy__KnownProxies__0` | IP fixo do proxy (nginx em outra máquina) |
| `ReverseProxy__ForwardLimit` | número de proxies seguidos; Cloudflare na frente do proxy da hospedagem = `2` |

Se chegar um `X-Forwarded-For` que a API não aceita, ela avisa no log dizendo o que configurar.

### 4. Frontend

| Variável | Quando | Valor |
|---|---|---|
| `NEXT_PUBLIC_API_URL` | **build** (obrigatória) | `https://api.vitrio.com.br` |
| `API_URL` | execução | URL da API vista pelo servidor do Next (vazio = a de cima) |
| `CLOUDINARY_CLOUD_NAME`, `CLOUDINARY_API_KEY`, `CLOUDINARY_API_SECRET` | execução | upload das imagens |
| `CLOUDINARY_FOLDER` | execução | a mesma pasta do `Cloudinary__Folder` do backend |
| `NEXT_PUBLIC_SUPPORT_EMAIL` | build | e-mail de suporte no rodapé e na tela de Suporte (vazio = não mostra; o build avisa) |
| `SITE_URL` | execução | `https://vitrio.com.br`, para a imagem de prévia do link da loja (vazio = o endereço pelo qual a página foi pedida) |

```bash
npm ci
npm run build   # recusa rodar sem NEXT_PUBLIC_API_URL
npm start
```

- `NEXT_PUBLIC_API_URL` fica fixada no build. Por isso, mudar a URL da API exige um build novo, e uma imagem Docker não serve para dois ambientes.
- O navegador reduz as fotos (no máximo 2000 px, em WebP) antes do upload, que aceita até 4 MB. Isso fica abaixo do limite de 4,5 MB por requisição da Vercel.
- O plano gratuito da Vercel (Hobby) é só para uso não comercial; o Vitrio cobra assinaturas.
- O link da loja (`/store/{slug}`) sai com o nome, a descrição e uma imagem da loja na prévia do WhatsApp, do Instagram e do Telegram (`app/store/[slug]/client/layout.tsx` e `opengraph-image.tsx`).

### 5. Mercado Pago

1. Ative as credenciais de produção dos dois apps (assinaturas e marketplace).
2. No app das assinaturas, em Webhooks, cadastre `https://api.vitrio.com.br/api/Webhooks/mercadopago` e copie a assinatura secreta para `MercadoPago__WebhookSecret`.
3. No app do marketplace, cadastre a URL de redirecionamento `https://api.vitrio.com.br/api/StorePayment/oauth/callback`.
4. Faça um teste real com valor baixo: uma assinatura, um pedido pago online e um estorno (cancelando o pedido).

Para o Pix aparecer no checkout, a conta do Mercado Pago conectada à loja precisa ter uma chave Pix.

### 6. E-mail

No Resend, verifique o domínio (registros DNS) e use um endereço dele em `Email__From`. O `onboarding@resend.dev` só entrega para o e-mail do dono da conta do Resend.

O plano gratuito do Resend envia 3.000 e-mails por mês, no máximo 100 por dia (confirmação de cadastro, senha e avisos de pedido somados). Acima disso os envios param até o dia seguinte; o plano pago começa em US$ 20 por mês.

### 7. Monitoramento

- `GET https://api.vitrio.com.br/health` responde `Healthy` quando a API está no ar e o banco responde (`Unhealthy` e HTTP 503, senão). Use-o:
  - na verificação de saúde da hospedagem;
  - num monitor gratuito (UptimeRobot, Better Stack), que manda e-mail quando cai.
- Para receber os erros por e-mail, configure `Sentry__Dsn` (seção 2).

### 8. Admin

O primeiro Admin é definido direto no banco, depois de a pessoa criar a conta de lojista:

```sql
UPDATE "User" SET "Role" = 1 WHERE "Email" = 'voce@exemplo.com';  -- 1 = Admin
```

Ela entra de novo e passa a ver o item **Admin** no menu (`/menu/admin`).

### 9. Antes de abrir ao público

- Publique os termos de uso e a política de privacidade (LGPD), e ofereça um jeito de o usuário apagar a conta e os dados. Depois, ponha os links no rodapé (`footerColumns` em `components/Home/Home.tsx`).
- Ao mudar a página inicial (`components/Home/Home.tsx`), descreva só o que o sistema já faz. Propaganda que não corresponde ao produto é proibida pelo Código de Defesa do Consumidor. Os planos e preços dela vêm da API, então mudam junto com o banco.
- Teste o fluxo completo no domínio de produção: cadastro e confirmação de e-mail, loja, produto, pedido com entrega, pagamento, e-mails e recuperação de senha.
