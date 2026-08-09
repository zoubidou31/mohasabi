import { useEffect, useState } from 'react';
import {
  Alert,
  Box,
  Button,
  Card,
  CardContent,
  Divider,
  FormControl,
  Grid,
  IconButton,
  InputLabel,
  MenuItem,
  Select,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  TextField,
  Typography,
} from '@mui/material';
import { ArrowLeft, Plus, Save, Trash2 } from 'lucide-react';
import { useNavigate, useParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { api, extractError } from '../api/client';
import type { Client, Invoice, InvoiceType, PaymentMethod, Product, TVARate } from '../api/types';
import { formatCurrency } from '../utils/format';
import PageHeader from '../components/PageHeader';

interface LineForm {
  productId?: string;
  reference: string;
  description: string;
  quantity: number;
  unitPriceHT: number;
}

const round2 = (value: number) => Math.round(value * 100) / 100;

function lineTotal(line: LineForm): number {
  return round2(line.quantity * line.unitPriceHT);
}

export default function InvoiceFormPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { id } = useParams<{ id: string }>();
  const isEdit = Boolean(id);

  const [clients, setClients] = useState<Client[]>([]);
  const [products, setProducts] = useState<Product[]>([]);
  const [clientId, setClientId] = useState('');
  const [invoiceDate, setInvoiceDate] = useState(new Date().toISOString().slice(0, 10));
  const [validityDays, setValidityDays] = useState(30);
  const [invoiceType, setInvoiceType] = useState<InvoiceType>('Facture');
  const [paymentMethod, setPaymentMethod] = useState<PaymentMethod>('Comptant');
  const [orderReference, setOrderReference] = useState('');
  const [notes, setNotes] = useState('');
  const [vatRate, setVatRate] = useState<0.19 | 0.09>(0.19);
  const [lines, setLines] = useState<LineForm[]>([
    { reference: '', description: '', quantity: 1, unitPriceHT: 0 },
  ]);
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    Promise.all([api.get<Client[]>('/clients'), api.get<Product[]>('/products?includeInactive=true')])
      .then(([c, p]) => {
        setClients(c.data);
        setProducts(p.data);
      })
      .catch(() => {});
  }, []);

  useEffect(() => {
    if (!isEdit || !id) return;
    api
      .get<Invoice>(`/invoices/${id}`)
      .then(({ data }) => {
        setClientId(data.clientId);
        setInvoiceDate(data.invoiceDate.slice(0, 10));
        setValidityDays(data.validityDays);
        setInvoiceType(data.invoiceType);
        setPaymentMethod(data.paymentMethod);
        setOrderReference(data.orderReference ?? '');
        setNotes(data.notes ?? '');
        setVatRate(data.lines[0]?.tvaRate === 'Reduit' ? 0.09 : 0.19);
        setLines(
          data.lines.map((l) => ({
            productId: l.productId,
            reference: l.reference,
            description: l.description,
            quantity: l.quantity,
            unitPriceHT: l.unitPriceHT,
          })),
        );
      })
      .catch(() => {});
  }, [isEdit, id]);

  const updateLine = (index: number, patch: Partial<LineForm>) => {
    setLines((prev) => prev.map((line, i) => (i === index ? { ...line, ...patch } : line)));
  };

  const pickProduct = (index: number, productId: string) => {
    const product = products.find((p) => p.id === productId);
    updateLine(index, {
      productId,
      reference: product?.reference ?? '',
      description: product?.name ?? '',
      unitPriceHT: product?.defaultPrice ?? 0,
    });
  };

  const totalHT = round2(lines.reduce((sum, l) => sum + lineTotal(l), 0));
  const totalVAT = round2(lines.reduce((sum, l) => sum + round2(lineTotal(l) * vatRate), 0));
  const totalTTC = round2(totalHT + totalVAT);

  const submit = async () => {
    if (!clientId) {
      setError(t('invoice.client') + ' : ' + t('common.none'));
      return;
    }
    if (lines.length === 0 || lines.every((l) => l.quantity <= 0)) {
      setError(t('invoice.lines') + ' : ' + t('common.none'));
      return;
    }
    setBusy(true);
    setError('');
    const lineTvaRate: TVARate = vatRate === 0.09 ? 'Reduit' : 'Normal';
    const payload = {
      clientId,
      invoiceDate,
      validityDays,
      invoiceType,
      paymentMethod,
      orderReference: orderReference || null,
      notes: notes || null,
      remiseValue: null,
      remiseIsPercentage: true,
      fraisPort: null,
      autresFrais: null,
      lines: lines.map((l) => ({
        productId: l.productId ?? null,
        reference: l.reference,
        description: l.description,
        quantity: l.quantity,
        unitPriceHT: l.unitPriceHT,
        tvaRate: lineTvaRate,
      })),
    };
    try {
      if (isEdit) {
        await api.put(`/invoices/${id}`, payload);
      } else {
        await api.post('/invoices', payload);
      }
      navigate(isEdit ? `/invoices/${id}` : '/invoices');
    } catch (err) {
      setError(extractError(err));
      setBusy(false);
    }
  };

  return (
    <Box>
      <PageHeader
        title={isEdit ? t('invoice.editInvoice') : t('invoice.newInvoice')}
        description={t('invoice.description')}
        action={
          <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, flexWrap: 'wrap' }}>
            <IconButton
              onClick={() => navigate(isEdit ? `/invoices/${id}` : '/invoices')}
              sx={{ backgroundColor: 'background.paper', border: '1px solid', borderColor: 'divider' }}
            >
              <ArrowLeft size={18} />
            </IconButton>
          </Box>
        }
      />

      {error && (
        <Alert severity="error" sx={{ mb: 3 }}>
          {error}
        </Alert>
      )}

      <Card sx={{ mb: 3 }}>
        <CardContent sx={{ p: 3 }}>
          <Typography variant="subtitle1" sx={{ fontWeight: 700, mb: 2 }}>
            {t('invoice.client')}
          </Typography>
          <Grid container spacing={2}>
            <Grid item xs={12} sm={6} md={3}>
              <FormControl fullWidth>
                <InputLabel>{t('invoice.client')}</InputLabel>
                <Select label={t('invoice.client')} value={clientId} onChange={(e) => setClientId(e.target.value)}>
                  {clients.map((c) => (
                    <MenuItem key={c.id} value={c.id}>
                      {c.displayName}
                    </MenuItem>
                  ))}
                </Select>
              </FormControl>
            </Grid>
            <Grid item xs={12} sm={6} md={3}>
              <TextField
                type="date"
                label={t('invoice.invoiceDate')}
                fullWidth
                InputLabelProps={{ shrink: true }}
                value={invoiceDate}
                onChange={(e) => setInvoiceDate(e.target.value)}
              />
            </Grid>
            <Grid item xs={6} sm={4} md={2}>
              <FormControl fullWidth>
                <InputLabel>{t('invoice.type')}</InputLabel>
                <Select label={t('invoice.type')} value={invoiceType} onChange={(e) => setInvoiceType(e.target.value as InvoiceType)}>
                  {(['Facture', 'ProForma', 'Avoir'] as InvoiceType[]).map((ty) => (
                    <MenuItem key={ty} value={ty}>
                      {t(`typeLabels.${ty}`)}
                    </MenuItem>
                  ))}
                </Select>
              </FormControl>
            </Grid>
            <Grid item xs={6} sm={4} md={2}>
              <FormControl fullWidth>
                <InputLabel>{t('invoice.paymentMethod')}</InputLabel>
                <Select label={t('invoice.paymentMethod')} value={paymentMethod} onChange={(e) => setPaymentMethod(e.target.value as PaymentMethod)}>
                  {(['Comptant', 'Cheque', 'VirementBancaire', 'CarteBancaire', 'Credit'] as PaymentMethod[]).map((pm) => (
                    <MenuItem key={pm} value={pm}>
                      {t(`paymentLabels.${pm}`)}
                    </MenuItem>
                  ))}
                </Select>
              </FormControl>
            </Grid>
            <Grid item xs={12} sm={4} md={2}>
              <TextField
                type="number"
                label={t('invoice.validityDays')}
                fullWidth
                value={validityDays}
                onChange={(e) => setValidityDays(parseInt(e.target.value, 10) || 0)}
              />
            </Grid>
            <Grid item xs={12}>
              <TextField
                label={t('invoice.orderReference')}
                fullWidth
                value={orderReference}
                onChange={(e) => setOrderReference(e.target.value)}
              />
            </Grid>
          </Grid>
        </CardContent>
      </Card>

      <Card sx={{ mb: 3 }}>
        <CardContent sx={{ p: 3 }}>
          <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 2 }}>
            <Typography variant="subtitle1" sx={{ fontWeight: 700 }}>
              {t('invoice.lines')}
            </Typography>
            <Button
              size="small"
              variant="outlined"
              startIcon={<Plus size={16} />}
              onClick={() => setLines((prev) => [...prev, { reference: '', description: '', quantity: 1, unitPriceHT: 0 }])}
            >
              {t('invoice.addLine')}
            </Button>
          </Box>

          <TableContainer component={Card} sx={{ boxShadow: 'none' }}>
            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell>{t('invoice.product')}</TableCell>
                  <TableCell>{t('common.description')}</TableCell>
                  <TableCell width={90} align="center">
                    {t('common.quantity')}
                  </TableCell>
                  <TableCell width={140} align="right">
                    {t('common.unitPrice')}
                  </TableCell>
                  <TableCell width={150} align="right">
                    {t('invoice.lineTotalHT')}
                  </TableCell>
                  <TableCell width={50} />
                </TableRow>
              </TableHead>
              <TableBody>
                {lines.map((line, index) => (
                  <TableRow key={index}>
                    <TableCell sx={{ minWidth: 180 }}>
                      <FormControl fullWidth size="small">
                        <Select
                          displayEmpty
                          value={line.productId ?? ''}
                          onChange={(e) => pickProduct(index, e.target.value)}
                        >
                          <MenuItem value="">{t('common.none')}</MenuItem>
                          {products.map((p) => (
                            <MenuItem key={p.id} value={p.id}>
                              {p.reference} — {p.name}
                            </MenuItem>
                          ))}
                        </Select>
                      </FormControl>
                    </TableCell>
                    <TableCell>
                      <TextField
                        size="small"
                        fullWidth
                        value={line.description}
                        onChange={(e) => updateLine(index, { description: e.target.value, reference: e.target.value })}
                      />
                    </TableCell>
                    <TableCell align="center">
                      <TextField
                        size="small"
                        type="number"
                        inputProps={{ min: 0, style: { textAlign: 'center' } }}
                        value={line.quantity}
                        onChange={(e) => updateLine(index, { quantity: parseFloat(e.target.value) || 0 })}
                      />
                    </TableCell>
                    <TableCell align="right" sx={{ whiteSpace: 'nowrap' }}>
                      <TextField
                        size="small"
                        type="number"
                        inputProps={{ min: 0, style: { textAlign: 'right' } }}
                        value={line.unitPriceHT}
                        onChange={(e) => updateLine(index, { unitPriceHT: parseFloat(e.target.value) || 0 })}
                      />
                    </TableCell>
                    <TableCell align="right" sx={{ whiteSpace: 'nowrap' }}>
                      <Typography variant="body2" fontWeight={600} className="tnum" sx={{ whiteSpace: 'nowrap' }}>
                        {formatCurrency(lineTotal(line))}
                      </Typography>
                    </TableCell>
                    <TableCell align="center">
                      <IconButton
                        size="small"
                        sx={{ color: 'text.secondary', '&:hover': { color: 'error.main', backgroundColor: 'error.light' } }}
                        onClick={() => setLines((prev) => prev.filter((_, i) => i !== index))}
                      >
                        <Trash2 size={16} />
                      </IconButton>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </TableContainer>

          <Box sx={{ display: 'flex', justifyContent: 'flex-end', mt: 2 }}>
            <Card sx={{ width: 360, p: 2.5, borderRadius: 3 }}>
              <Typography variant="subtitle1" fontWeight={700} sx={{ letterSpacing: 0.5 }}>
                {t('invoice.summary')}
              </Typography>
              <Divider sx={{ my: 1.5 }} />
              <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', py: 0.75 }}>
                <Typography variant="body2" color="text.secondary">
                  {t('invoice.totalHT')}
                </Typography>
                <Typography variant="body2" fontWeight={600} className="tnum">
                  {formatCurrency(totalHT)}
                </Typography>
              </Box>
              <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', py: 0.75 }}>
                <Typography variant="body2" color="text.secondary">
                  {t('invoice.vatRate')}
                </Typography>
                <FormControl size="small" sx={{ minWidth: 90 }}>
                  <Select value={vatRate} onChange={(e) => setVatRate(e.target.value as 0.19 | 0.09)}>
                    <MenuItem value={0.19}>19%</MenuItem>
                    <MenuItem value={0.09}>9%</MenuItem>
                  </Select>
                </FormControl>
              </Box>
              <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', py: 0.75 }}>
                <Typography variant="body2" color="text.secondary">
                  {t('invoice.vatAmount')}
                </Typography>
                <Typography variant="body2" fontWeight={600} className="tnum">
                  {formatCurrency(totalVAT)}
                </Typography>
              </Box>
              <Divider sx={{ my: 1 }} />
              <Box
                sx={{
                  display: 'flex',
                  justifyContent: 'space-between',
                  alignItems: 'center',
                  py: 1.25,
                  px: 1.5,
                  bgcolor: 'primary.light',
                  borderRadius: 2,
                }}
              >
                <Typography variant="subtitle1" fontWeight={700} sx={{ color: 'primary.dark' }}>
                  {t('invoice.totalTTC')}
                </Typography>
                <Typography variant="subtitle1" fontWeight={800} sx={{ color: 'primary.dark' }} className="tnum">
                  {formatCurrency(totalTTC)}
                </Typography>
              </Box>
            </Card>
          </Box>
        </CardContent>
      </Card>

      <Card sx={{ mb: 3 }}>
        <CardContent sx={{ p: 3 }}>
          <Typography variant="subtitle1" sx={{ fontWeight: 700, mb: 2 }}>
            {t('invoice.notes')}
          </Typography>
          <TextField label={t('invoice.notes')} fullWidth multiline rows={3} value={notes} onChange={(e) => setNotes(e.target.value)} />
        </CardContent>
      </Card>

      <Box sx={{ display: 'flex', justifyContent: 'flex-end', gap: 1.5 }}>
        <Button variant="outlined" onClick={() => navigate(isEdit ? `/invoices/${id}` : '/invoices')}>
          {t('common.cancel')}
        </Button>
        <Button variant="contained" startIcon={<Save size={18} />} disabled={busy} onClick={() => void submit()}>
          {t('common.save')}
        </Button>
      </Box>
    </Box>
  );
}
