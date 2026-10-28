import ConfirmEmail from "@/components/Auth/ConfirmEmail";

export default async function ConfirmEmailPage({
  searchParams,
}: {
  searchParams: Promise<{ [key: string]: string | string[] | undefined }>;
}) {
  const { token } = await searchParams;
  return <ConfirmEmail token={typeof token === "string" ? token : ""} />;
}
