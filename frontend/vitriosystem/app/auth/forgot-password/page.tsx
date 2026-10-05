import ForgotPassword from "@/components/Auth/ForgotPassword";
import { safeStoreSlug } from "@/lib/store_slug";

export default async function ForgotPasswordPage({
  searchParams,
}: {
  searchParams: Promise<{ [key: string]: string | string[] | undefined }>;
}) {
  const { store } = await searchParams;
  return <ForgotPassword storeSlug={safeStoreSlug(store)} />;
}
