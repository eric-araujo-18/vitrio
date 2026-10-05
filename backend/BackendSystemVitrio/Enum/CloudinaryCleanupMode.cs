namespace BackendSystemVitrio.Enum
{
    // O que a limpeza de imagens órfãs do Cloudinary faz (Cloudinary:CleanupMode).
    public enum CloudinaryCleanupMode
    {
        Off,     // não roda
        DryRun,  // só registra no log o que apagaria
        Delete   // apaga de verdade
    }
}
