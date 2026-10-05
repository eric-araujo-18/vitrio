import ResetPassword from "@/components/Auth/ResetPassword";
import { safeStoreSlug } from "@/lib/store_slug";

export default async function ResetPasswordPage({
  searchParams,
}: {
  searchParams: Promise<{ [key: string]: string | string[] | undefined }>;
}) {
  const { token, store } = await searchParams;
  return <ResetPassword token={typeof token === "string" ? token : ""} storeSlug={safeStoreSlug(store)} />;
}
