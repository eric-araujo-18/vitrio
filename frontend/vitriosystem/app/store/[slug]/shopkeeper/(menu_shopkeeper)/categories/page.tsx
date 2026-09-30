"use client";

import { Fragment, useCallback, useEffect, useMemo, useState, type FormEvent } from "react";
import { Check, CornerDownRight, LoaderCircle, Pencil, Plus, Tags, Trash2, X } from "lucide-react";
import { unwrap } from "@/lib/api";
import {
  createCategory,
  deleteCategory,
  getCategoriesByStore,
  updateCategory,
  type Category,
} from "@/lib/api_category";
import { useShopkeeperStore } from "../../components/ShopkeeperStoreContext";
import {
  EmptyState,
  ErrorBox,
  InlineConfirm,
  LoadingState,
  PageHeader,
  StatusBadge,
  btnIcon,
  btnPrimary,
  card,
  cardTitle,
  input,
  table,
  tableWrapper,
} from "../../components/Ui";

export default function CategoriesPage() {
  const { store } = useShopkeeperStore();

  const [categories, setCategories] = useState<Category[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Formulário de criação
  const [newName, setNewName] = useState("");
  const [newParent, setNewParent] = useState("");
  const [creating, setCreating] = useState(false);

  // Edição inline
  const [editingId, setEditingId] = useState<number | null>(null);
  const [editName, setEditName] = useState("");
  const [editParent, setEditParent] = useState("");
  const [savingEdit, setSavingEdit] = useState(false);

  // Exclusão (substitui o confirm() do navegador)
  const [confirmDeleteId, setConfirmDeleteId] = useState<number | null>(null);
  const [deleting, setDeleting] = useState(false);

  const [togglingId, setTogglingId] = useState<number | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setCategories(await unwrap(getCategoriesByStore(store.id)));
      setError(null);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Erro ao carregar categorias.");
    } finally {
      setLoading(false);
    }
  }, [store.id]);

  useEffect(() => {
    load();
  }, [load]);

  const byId = useMemo(() => new Map(categories.map((c) => [c.id, c])), [categories]);

  // Ordena como árvore: cada categoria raiz seguida das suas subcategorias.
  const ordered = useMemo(() => {
    const result: { category: Category; depth: number }[] = [];
    const walk = (parentId: number | null, depth: number) => {
      categories
        .filter((c) => c.parentCategoryId === parentId)
        .sort((a, b) => a.name.localeCompare(b.name))
        .forEach((c) => {
          result.push({ category: c, depth });
          if (depth < 5) walk(c.id, depth + 1);
        });
    };
    walk(null, 0);
    // Órfãs (pai excluído/inexistente) no fim, pra não sumirem da lista
    categories.forEach((c) => {
      if (!result.some((r) => r.category.id === c.id)) result.push({ category: c, depth: 0 });
    });
    return result;
  }, [categories]);

  async function handleCreate(e: FormEvent) {
    e.preventDefault();
    if (!newName.trim()) return;

    setCreating(true);
    setError(null);
    try {
      const created = await unwrap(
        createCategory({
          storeId: store.id,
          name: newName.trim(),
          parentCategoryId: newParent ? Number(newParent) : undefined,
        })
      );
      setCategories((prev) => [...prev, created]);
      setNewName("");
      setNewParent("");
    } catch (err) {
      setError(err instanceof Error ? err.message : "Erro ao criar categoria.");
    } finally {
      setCreating(false);
    }
  }

  function startEdit(c: Category) {
    setConfirmDeleteId(null);
    setEditingId(c.id);
    setEditName(c.name);
    setEditParent(c.parentCategoryId ? String(c.parentCategoryId) : "");
  }

  async function saveEdit(c: Category) {
    if (!editName.trim()) return;
    setError(null);
    setSavingEdit(true);
    try {
      const updated = await unwrap(
        updateCategory(c.id, {
          name: editName.trim(),
          parentCategoryId: editParent ? Number(editParent) : undefined,
          removeParent: !editParent,
        })
      );
      setCategories((prev) => prev.map((x) => (x.id === c.id ? updated : x)));
      setEditingId(null);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Erro ao salvar categoria.");
    } finally {
      setSavingEdit(false);
    }
  }

  async function toggleActive(c: Category) {
    setTogglingId(c.id);
    try {
      const updated = await unwrap(updateCategory(c.id, { isActive: !c.isActive }));
      setCategories((prev) => prev.map((x) => (x.id === c.id ? updated : x)));
    } catch (err) {
      setError(err instanceof Error ? err.message : "Erro ao atualizar categoria.");
    } finally {
      setTogglingId(null);
    }
  }

  async function handleDelete(c: Category) {
    setDeleting(true);
    try {
      await unwrap(deleteCategory(c.id));
      setConfirmDeleteId(null);
      // Recarrega: subcategorias mudam de pai no backend.
      await load();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Erro ao excluir categoria.");
    } finally {
      setDeleting(false);
    }
  }

  return (
    <>
      <PageHeader
        title="Categorias"
        subtitle="Organize os produtos da vitrine. Subcategorias são opcionais."
      />

      {/* Nova categoria */}
      <section className={`${card} mb-6`}>
        <h2 className={cardTitle}>Nova categoria</h2>
        <form onSubmit={handleCreate} className="mt-4 flex flex-col gap-2 md:flex-row">
          <input
            aria-label="Nome da nova categoria"
            placeholder="Ex: Roupas masculinas"
            value={newName}
            onChange={(e) => setNewName(e.target.value)}
            maxLength={80}
            className={`${input} h-10 flex-1 py-0`}
          />
          <select
            aria-label="Categoria pai"
            value={newParent}
            onChange={(e) => setNewParent(e.target.value)}
            className={`${input} h-10 py-0 md:w-56`}
          >
            <option value="">Categoria principal</option>
            {categories.map((c) => (
              <option key={c.id} value={c.id}>
                Dentro de: {c.name}
              </option>
            ))}
          </select>
          <button type="submit" disabled={creating || !newName.trim()} className={btnPrimary}>
            {creating ? (
              <LoaderCircle size={16} aria-hidden="true" className="animate-spin" />
            ) : (
              <Plus size={16} aria-hidden="true" />
            )}
            {creating ? "Criando..." : "Adicionar"}
          </button>
        </form>
      </section>

      {error && (
        <div className="mb-4">
          <ErrorBox>{error}</ErrorBox>
        </div>
      )}

      {loading ? (
        <LoadingState>Carregando categorias...</LoadingState>
      ) : categories.length === 0 ? (
        <EmptyState icon={<Tags size={26} aria-hidden="true" />}>
          Nenhuma categoria ainda. Crie a primeira acima.
        </EmptyState>
      ) : (
        <div className={tableWrapper}>
          <table className={table}>
            <thead>
              <tr>
                <th>Nome</th>
                <th>Slug</th>
                <th>Produtos</th>
                <th>Status</th>
                <th className="w-[110px]">
                  <span className="sr-only">Ações</span>
                </th>
              </tr>
            </thead>
            <tbody>
              {ordered.map(({ category: c, depth }) => {
                const isEditing = editingId === c.id;
                const isConfirmingDelete = confirmDeleteId === c.id;

                return (
                  <Fragment key={c.id}>
                    <tr className={isConfirmingDelete ? "bg-red-50/40" : ""}>
                      <td>
                        {/* Recuo de subcategoria: valor dinâmico, por isso fica em style */}
                        <div style={{ paddingLeft: `${depth * 1.4}rem` }}>
                          {isEditing ? (
                            <div className="flex flex-wrap gap-2">
                              <input
                                aria-label="Nome da categoria"
                                value={editName}
                                onChange={(e) => setEditName(e.target.value)}
                                onKeyDown={(e) => {
                                  if (e.key === "Enter") saveEdit(c);
                                  if (e.key === "Escape") setEditingId(null);
                                }}
                                autoFocus
                                disabled={savingEdit}
                                className={`${input} h-9 min-w-40 flex-1 py-0`}
                              />
                              <select
                                aria-label="Categoria pai"
                                value={editParent}
                                onChange={(e) => setEditParent(e.target.value)}
                                disabled={savingEdit}
                                className={`${input} h-9 w-auto py-0`}
                              >
                                <option value="">Categoria principal</option>
                                {categories
                                  .filter((x) => x.id !== c.id)
                                  .map((x) => (
                                    <option key={x.id} value={x.id}>
                                      Dentro de: {x.name}
                                    </option>
                                  ))}
                              </select>
                            </div>
                          ) : (
                            <span className="flex items-center gap-1.5">
                              {depth > 0 && (
                                <CornerDownRight size={14} aria-hidden="true" className="shrink-0 text-outline" />
                              )}
                              <strong className="text-on-surface">{c.name}</strong>
                              {c.parentCategoryId && !byId.has(c.parentCategoryId) && (
                                <span className="text-body-sm text-outline">(pai removido)</span>
                              )}
                            </span>
                          )}
                        </div>
                      </td>
                      <td>
                        <span className="font-mono text-code-sm text-outline">{c.slug}</span>
                      </td>
                      <td>
                        <span className="tabular-nums">{c.productCount}</span>
                      </td>
                      <td>
                        <button
                          type="button"
                          onClick={() => toggleActive(c)}
                          disabled={togglingId === c.id}
                          title="Clique para alternar"
                          aria-label={`${c.isActive ? "Desativar" : "Ativar"} categoria ${c.name}`}
                          className="rounded-full transition-opacity hover:opacity-75 disabled:opacity-50"
                        >
                          <StatusBadge status={c.isActive ? "Active" : "Inactive"}>
                            {c.isActive ? "Ativa" : "Inativa"}
                          </StatusBadge>
                        </button>
                      </td>
                      <td>
                        <div className="flex justify-end gap-1.5">
                          {isEditing ? (
                            <>
                              <button
                                type="button"
                                onClick={() => saveEdit(c)}
                                disabled={savingEdit || !editName.trim()}
                                aria-label="Salvar"
                                title="Salvar"
                                className={`${btnIcon} text-emerald-600! hover:text-emerald-700!`}
                              >
                                {savingEdit ? (
                                  <LoaderCircle size={15} aria-hidden="true" className="animate-spin" />
                                ) : (
                                  <Check size={15} aria-hidden="true" />
                                )}
                              </button>
                              <button
                                type="button"
                                onClick={() => setEditingId(null)}
                                disabled={savingEdit}
                                aria-label="Cancelar edição"
                                title="Cancelar"
                                className={btnIcon}
                              >
                                <X size={15} aria-hidden="true" />
                              </button>
                            </>
                          ) : (
                            <>
                              <button
                                type="button"
                                onClick={() => startEdit(c)}
                                aria-label={`Editar ${c.name}`}
                                title="Editar"
                                className={btnIcon}
                              >
                                <Pencil size={15} aria-hidden="true" />
                              </button>
                              <button
                                type="button"
                                onClick={() => {
                                  setEditingId(null);
                                  setConfirmDeleteId(c.id);
                                }}
                                aria-label={`Excluir ${c.name}`}
                                title="Excluir"
                                className={`${btnIcon} hover:border-red-200! hover:bg-red-50! hover:text-red-600!`}
                              >
                                <Trash2 size={15} aria-hidden="true" />
                              </button>
                            </>
                          )}
                        </div>
                      </td>
                    </tr>

                    {isConfirmingDelete && (
                      <tr className="hover:bg-transparent!">
                        <td colSpan={5} className="bg-red-50/40">
                          <InlineConfirm
                            message={
                              <>
                                Excluir <strong>{c.name}</strong>?
                                {c.productCount > 0 &&
                                  ` ${c.productCount} produto(s) ficarão sem categoria.`}
                              </>
                            }
                            confirmLabel="Excluir"
                            busy={deleting}
                            onConfirm={() => handleDelete(c)}
                            onCancel={() => setConfirmDeleteId(null)}
                          />
                        </td>
                      </tr>
                    )}
                  </Fragment>
                );
              })}
            </tbody>
          </table>
        </div>
      )}
    </>
  );
}