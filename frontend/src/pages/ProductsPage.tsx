import { useCallback, useEffect, useMemo, useState } from 'react';
import {
  Autocomplete,
  Box,
  Button,
  Card,
  Checkbox,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  FormControlLabel,
  IconButton,
  MenuItem,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TablePagination,
  TableRow,
  TextField,
  Typography,
} from '@mui/material';
import { Package, Pencil, Plus, Search, Trash2, Wrench } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { api, extractError } from '../api/client';
import type { Product, TVARate, Category, PagedResult } from '../api/types';
import { formatCurrency } from '../utils/format';
import PageHeader from '../components/PageHeader';
import StatusBadge from '../components/StatusBadge';
import { validateProductReference, validateProductName, validateProductPrice, type ValidationErrors } from '../utils/companyValidation';

const emptyForm = {
  reference: '',
  name: '',
  description: '',
  categoryId: '',
  defaultPrice: 0,
  defaultTvaRate: 'Normal' as TVARate,
  isService: false,
  isActive: true,
};

export default function ProductsPage() {
  const { t } = useTranslation();
  const [data, setData] = useState<PagedResult<Product> | null>(null);
  const [categories, setCategories] = useState<Category[]>([]);
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(0);
  const [pageSize, setPageSize] = useState(20);
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editing, setEditing] = useState<Product | null>(null);
  const [form, setForm] = useState(emptyForm);
  const [error, setError] = useState('');
  const [fieldErrors, setFieldErrors] = useState<ValidationErrors>({});
  const [reload, setReload] = useState(0);

  const [catDialogOpen, setCatDialogOpen] = useState(false);
  const [catForm, setCatForm] = useState({ name: '', description: '' });
  const [catError, setCatError] = useState('');
  const [catFieldErrors, setCatFieldErrors] = useState<Record<string, string>>({});
  const [catSearch, setCatSearch] = useState('');

  const load = useCallback(async () => {
    try {
      const { data: d } = await api.get<PagedResult<Product>>('/products', {
        params: { includeInactive: true, search: search || undefined, page: page + 1, pageSize },
      });
      setData(d);
    } catch {
      // handled silently
    }
  }, [search, page, pageSize]);

  const loadCategories = useCallback(async () => {
    try {
      const { data } = await api.get<Category[]>('/categories?active=true');
      setCategories(data);
    } catch {
      // handled silently
    }
  }, []);

  useEffect(() => {
    void load();
    void loadCategories();
  }, [load, loadCategories, reload]);

  const openNew = () => {
    setEditing(null);
    setForm(emptyForm);
    setError('');
    setFieldErrors({});
    setDialogOpen(true);
  };

  const openEdit = (p: Product) => {
    setEditing(p);
    setForm({
      reference: p.reference,
      name: p.name,
      description: p.description ?? '',
      categoryId: p.categoryId ?? '',
      defaultPrice: p.defaultPrice,
      defaultTvaRate: p.defaultTvaRate,
      isService: p.isService,
      isActive: p.isActive,
    });
    setError('');
    setFieldErrors({});
    setDialogOpen(true);
  };

  const save = async () => {
    setError('');
    const errs: ValidationErrors = {};
    const refErr = validateProductReference(form.reference);
    if (refErr) errs.reference = refErr;
    const nameErr = validateProductName(form.name);
    if (nameErr) errs.name = nameErr;
    const priceErr = validateProductPrice(form.defaultPrice);
    if (priceErr) errs.defaultPrice = priceErr;
    setFieldErrors(errs);
    if (Object.keys(errs).length > 0) return;
    try {
      const payload = {
        ...form,
        category: categories.find((c) => c.id === form.categoryId)?.name ?? '',
      };
      if (editing) {
        await api.put(`/products/${editing.id}`, payload);
      } else {
        await api.post('/products', payload);
      }
      setDialogOpen(false);
      setReload((x) => x + 1);
    } catch (err) {
      setError(extractError(err));
    }
  };

  const remove = async (p: Product) => {
    try {
      await api.delete(`/products/${p.id}`);
      setReload((x) => x + 1);
    } catch {
      // handled silently; keeps row
    }
  };

  const saveCategory = async () => {
    setCatError('');
    const errs: Record<string, string> = {};
    const name = catForm.name.trim();
    if (!name || name.length < 2) errs.name = 'Le nom est obligatoire (min. 2 caractères).';
    if (name.length > 100) errs.name = 'Le nom ne peut pas dépasser 100 caractères.';
    if (categories.some((c) => c.name.toLowerCase() === name.toLowerCase())) {
      errs.name = 'Cette catégorie existe déjà.';
    }
    setCatFieldErrors(errs);
    if (Object.keys(errs).length > 0) return;

    try {
      const { data } = await api.post<{ id: string }>('/categories', {
        name,
        description: catForm.description.trim() || null,
      });
      await loadCategories();
      setForm((f) => ({ ...f, categoryId: data.id }));
      setCatDialogOpen(false);
      setCatForm({ name: '', description: '' });
    } catch (err) {
      setCatError(extractError(err));
    }
  };

  const set = (field: keyof typeof emptyForm, value: string | number | boolean) => setForm((f) => ({ ...f, [field]: value }));

  const filteredCategories = useMemo(() => {
    if (!catSearch) return categories;
    const s = catSearch.toLowerCase();
    return categories.filter((c) => c.name.toLowerCase().includes(s));
  }, [categories, catSearch]);

  return (
    <Box>
      <PageHeader
        title={t('product.title')}
        description={t('product.description')}
        action={
          <Button variant="contained" startIcon={<Plus size={18} />} onClick={openNew}>
            {t('product.newProduct')}
          </Button>
        }
      />

      <Card sx={{ p: 2, mb: 3 }}>
        <TextField
          label={t('common.search')}
          value={search}
          onChange={(e) => {
            setSearch(e.target.value);
            setPage(0);
          }}
          size="small"
          placeholder={t('product.name')}
          sx={{ minWidth: 300, maxWidth: 420 }}
        />
      </Card>

      <TableContainer component={Card} sx={{ boxShadow: 'none' }}>
        <Table size="medium">
          <TableHead>
            <TableRow>
              <TableCell>{t('common.reference')}</TableCell>
              <TableCell>{t('product.name')}</TableCell>
              <TableCell>{t('product.category')}</TableCell>
              <TableCell align="right">{t('product.price')}</TableCell>
              <TableCell>{t('product.tvaRate')}</TableCell>
              <TableCell>{t('product.isService')}</TableCell>
              <TableCell>{t('common.status')}</TableCell>
              <TableCell align="right">{t('common.actions')}</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {data?.items.map((p) => (
              <TableRow key={p.id} hover>
                <TableCell sx={{ color: 'text.secondary', fontSize: 13 }}>{p.reference}</TableCell>
                <TableCell>
                  <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.25 }}>
                    <Box
                      sx={{
                        width: 32,
                        height: 32,
                        borderRadius: 2,
                        display: 'grid',
                        placeItems: 'center',
                        backgroundColor: 'primary.light',
                        color: 'primary.dark',
                        flexShrink: 0,
                      }}
                    >
                      {p.isService ? <Wrench size={15} /> : <Package size={15} />}
                    </Box>
                    <Typography variant="body2" fontWeight={600}>
                      {p.name}
                    </Typography>
                  </Box>
                </TableCell>
                <TableCell>{p.categoryName ?? p.category ?? '—'}</TableCell>
                <TableCell align="right" className="tnum" sx={{ fontWeight: 600 }}>{formatCurrency(p.defaultPrice)}</TableCell>
                <TableCell>
                  <StatusBadge variant="neutral" label={t(`tvaLabels.${p.defaultTvaRate}`)} />
                </TableCell>
                <TableCell>{p.isService ? t('common.yes') : t('common.no')}</TableCell>
                <TableCell>
                  <StatusBadge variant={p.isActive ? 'success' : 'neutral'} label={p.isActive ? t('product.active') : t('product.inactive')} />
                </TableCell>
                <TableCell align="right">
                  <Box sx={{ display: 'inline-flex', gap: 0.5 }}>
                    <IconButton size="small" onClick={() => openEdit(p)}>
                      <Pencil size={16} />
                    </IconButton>
                    <IconButton size="small" sx={{ color: 'text.secondary', '&:hover': { color: 'error.main' } }} onClick={() => void remove(p)}>
                      <Trash2 size={16} />
                    </IconButton>
                  </Box>
                </TableCell>
              </TableRow>
            ))}
            {data && data.items.length === 0 && (
              <TableRow>
                <TableCell colSpan={8} align="center" sx={{ py: 6, color: 'text.secondary' }}>
                  <Search size={28} style={{ opacity: 0.4, marginBottom: 8 }} />
                  <Typography variant="body2">{t('common.none')}</Typography>
                </TableCell>
              </TableRow>
            )}
          </TableBody>
        </Table>
        <TablePagination
          component="div"
          count={data?.totalCount ?? 0}
          page={page}
          onPageChange={(_, p) => setPage(p)}
          rowsPerPage={pageSize}
          onRowsPerPageChange={(e) => {
            setPageSize(parseInt(e.target.value, 10));
            setPage(0);
          }}
          labelRowsPerPage={t('common.filter')}
          rowsPerPageOptions={[10, 20, 50, 100]}
          sx={{ borderTop: '1px solid', borderColor: 'divider' }}
        />
      </TableContainer>

      {/* ── Product dialog ── */}
      <Dialog open={dialogOpen} onClose={() => setDialogOpen(false)} fullWidth maxWidth="sm">
        <DialogTitle>{editing ? t('common.edit') : t('product.newProduct')}</DialogTitle>
        <DialogContent>
          {error && (
            <Typography color="error" variant="body2" sx={{ mb: 1 }}>
              {error}
            </Typography>
          )}
          <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2, mt: 1 }}>
            <Box sx={{ display: 'flex', gap: 2 }}>
              <TextField
                label={t('common.reference')}
                fullWidth
                required
                value={form.reference}
                onChange={(e) => set('reference', e.target.value)}
                error={!!fieldErrors.reference}
                helperText={fieldErrors.reference}
              />
              <TextField
                label={t('product.name')}
                fullWidth
                required
                value={form.name}
                onChange={(e) => set('name', e.target.value)}
                error={!!fieldErrors.name}
                helperText={fieldErrors.name}
              />
            </Box>
            <TextField label={t('common.description')} fullWidth multiline rows={2} value={form.description} onChange={(e) => set('description', e.target.value)} />

            {/* ── Category dropdown with search + add ── */}
            <Autocomplete
              options={filteredCategories}
              getOptionLabel={(opt) => opt.name}
              isOptionEqualToValue={(opt, val) => opt.id === val.id}
              value={categories.find((c) => c.id === form.categoryId) ?? null}
              onChange={(_e, val) => {
                set('categoryId', val?.id ?? '');
              }}
              filterOptions={(opts, state) => {
                const input = state.inputValue.toLowerCase();
                return opts.filter((o) => o.name.toLowerCase().includes(input));
              }}
              renderInput={(params) => (
                <TextField
                  {...params}
                  label={t('product.category')}
                  placeholder={t('categories.searchPlaceholder', 'Rechercher une catégorie...')}
                />
              )}
              renderOption={(props, option) => (
                <li {...props} key={option.id}>
                  <Typography variant="body2">{option.name}</Typography>
                </li>
              )}
              ListboxProps={{
                sx: { maxHeight: 260 },
              }}
            />

            {/* Add category button at bottom of dropdown */}
            <Box sx={{ display: 'flex', justifyContent: 'flex-end' }}>
              <Button
                size="small"
                startIcon={<Plus size={14} />}
                sx={{ color: 'success.main', textTransform: 'none', fontWeight: 600 }}
                onClick={() => {
                  setCatForm({ name: '', description: '' });
                  setCatError('');
                  setCatFieldErrors({});
                  setCatSearch('');
                  setCatDialogOpen(true);
                }}
              >
                {t('categories.newCategory', '+ Ajouter une catégorie')}
              </Button>
            </Box>

            <Box sx={{ display: 'flex', gap: 2 }}>
              <TextField
                label={t('product.price')}
                type="number"
                fullWidth
                required
                value={form.defaultPrice}
                onChange={(e) => set('defaultPrice', parseFloat(e.target.value) || 0)}
                inputProps={{ min: 0 }}
                error={!!fieldErrors.defaultPrice}
                helperText={fieldErrors.defaultPrice}
              />
              <TextField select label={t('product.tvaRate')} fullWidth value={form.defaultTvaRate} onChange={(e) => set('defaultTvaRate', e.target.value)}>
                {(['Normal', 'Reduit', 'Exonere', 'IFU'] as TVARate[]).map((rate) => (
                  <MenuItem key={rate} value={rate}>
                    {t(`tvaLabels.${rate}`)}
                  </MenuItem>
                ))}
              </TextField>
            </Box>
            <FormControlLabel
              control={<Checkbox checked={form.isService} onChange={(e) => set('isService', e.target.checked)} />}
              label={t('product.isService')}
            />
            <FormControlLabel
              control={<Checkbox checked={form.isActive} onChange={(e) => set('isActive', e.target.checked)} />}
              label={t('product.active')}
            />
          </Box>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setDialogOpen(false)}>{t('common.cancel')}</Button>
          <Button variant="contained" onClick={() => void save()}>
            {t('common.save')}
          </Button>
        </DialogActions>
      </Dialog>

      {/* ── New category dialog ── */}
      <Dialog open={catDialogOpen} onClose={() => setCatDialogOpen(false)} fullWidth maxWidth="xs">
        <DialogTitle>{t('categories.newCategory', 'Nouvelle catégorie')}</DialogTitle>
        <DialogContent>
          {catError && (
            <Typography color="error" variant="body2" sx={{ mb: 1 }}>
              {catError}
            </Typography>
          )}
          <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2, mt: 1 }}>
            <TextField
              label={t('categories.name', 'Nom de la catégorie')}
              fullWidth
              required
              autoFocus
              value={catForm.name}
              onChange={(e) => setCatForm((f) => ({ ...f, name: e.target.value }))}
              error={!!catFieldErrors.name}
              helperText={catFieldErrors.name}
            />
            <TextField
              label={t('common.description', 'Description')}
              fullWidth
              multiline
              rows={2}
              value={catForm.description}
              onChange={(e) => setCatForm((f) => ({ ...f, description: e.target.value }))}
            />
          </Box>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setCatDialogOpen(false)}>{t('common.cancel', 'Annuler')}</Button>
          <Button variant="contained" onClick={() => void saveCategory()}>
            {t('common.add', 'Ajouter')}
          </Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
}
