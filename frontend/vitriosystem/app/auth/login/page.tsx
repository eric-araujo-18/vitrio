import Login from "@/components/Auth/Login";

export default async function LoginPage({
  searchParams,
}: {
  searchParams: Promise<{ [key: string]: string | string[] | undefined }>;
}) {
  // ?cadastro=1: acabou de criar a conta (Register).
  const { cadastro } = await searchParams;
  return <Login justRegistered={cadastro === "1"} />;
}