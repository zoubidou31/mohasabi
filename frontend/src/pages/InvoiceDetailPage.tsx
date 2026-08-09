import { useCallback, useEffect, useState } from 'react';
import {
  Alert,
  Box,
  Button,
  Card,
  CardContent,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Divider,
  Grid,
  IconButton,
  MenuItem,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  TextField,
  Typography,
} from '@mui/material';
import { ArrowLeft, Copy, CreditCard, Download, FilePlus2, Pencil } from 'lucide-react';
import { useNavigate, useParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { api, extractError } from '../api/client';
import type { Invoice, PaymentMethod } from '../api/types';
import { formatCurrency, formatDate } from '../utils/format';
import PageHeader from '../components/PageHeader';
import StatusBadge from '../components/StatusBadge';

export default function InvoiceDetailPage() {
  const { t, i18n } = useTranslation();
  const navigate = useNavigate();
  const { id } = useParams<{ id: string }>();
  const [invoice, setInvoice] = useState<Invoice | null>(null);
  const [error, setError] = useState('');
  const [loadError, setLoadError] = useState('');
  const [payOpen, setPayOpen] = useState(false);
  const [payAmount, setPayAmount] = useState('');
  const [payMethod, setPayMethod] = useState<PaymentMethod>('Comptant');

  const load = useCallback(async () => {
    if (!id) return;
    try {
      const { data } = await api.get<Invoice>(`/invoices/${id}`);
      setInvoice(data);
      setLoadError('');
    } catch (err) {
      setLoadError(extractError(err));
    }
  }, [id]);

  useEffect(() => {
    void load();
  }, [load]);

  const action = async (fn: () => Promise<unknown>) => {
    setError('');
    try {
      await fn();
      await load();
    } catch (err) {
      setError(extractError(err));
    }
  };

  const download = async (kind: 'pdf' | 'xlsx' | 'docx') => {
    const lang = i18n.language?.toLowerCase().startsWith('en') ? 'en' : 'fr';
    const resp = await api.get(`/invoices/${id}/export/${kind}`, { params: { lang }, responseType: 'blob' });
    const url = URL.createObjectURL(resp.data);
    const a = document.createElement('a');
    a.href = url;
    a.download = `${invoice?.invoiceNumber ?? 'facture'}.${kind === 'docx' ? 'docx' : kind === 'xlsx' ? 'xlsx' : 'pdf'}`;
    a.click();
    URL.revokeObjectURL(url);
  };

  const submitPayment = async () => {
    await action(() => api.post(`/invoices/${id}/payments`, { amount: parseFloat(payAmount) || 0, paymentMethod: payMethod }));
    setPayOpen(false);
    setPayAmount('');
  };

  if (!invoice) {
    if (loadError) {
      return (
        <Box>
          <PageHeader title="Facture" description="" />
          <Alert severity="error" sx={{ mb: 2 }}>
            {loadError}
          </Alert>
          <Button variant="outlined" onClick={() => void load()}>Réessayer</Button>
        </Box>
      );
    }
    return <Typography>{t('common.loading')}</Typography>;
  }

  const isDraft = invoice.status === 'Brouillon';
  const canPay = invoice.status !== 'Payee' && invoice.status !== 'Annulee' && invoice.soldeRestant > 0;
  const isFacture = invoice.invoiceType === 'Facture';

  return (
    <Box>
      <PageHeader
        title={invoice.invoiceNumber}
        description={invoice.clientName}
        action={
          <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, flexWrap: 'wrap' }}>
            <IconButton
              onClick={() => navigate('/invoices')}
              sx={{ backgroundColor: 'background.paper', border: '1px solid', borderColor: 'divider' }}
            >
              <ArrowLeft size={18} />
            </IconButton>
            <StatusBadge label={t(`statusLabels.${invoice.status}`)} />
          </Box>
        }
      />

      {error && (
        <Alert severity="error" sx={{ mb: 3 }}>
          {error}
        </Alert>
      )}

      <Grid container spacing={3}>
        <Grid item xs={12} lg={8}>
          <Card sx={{ mb: 3 }}>
            <CardContent sx={{ p: 3 }}>
              <Typography variant="subtitle1" sx={{ fontWeight: 700, mb: 2 }}>
                {t('invoice.title')}
              </Typography>
              <Grid container spacing={2}>
                <Grid item xs={6} md={4}>
                  <Typography variant="caption" color="text.secondary">
                    {t('invoice.client')}
                  </Typography>
                  <Typography variant="body1" fontWeight={600}>
                    {invoice.clientName}
                  </Typography>
                </Grid>
                <Grid item xs={6} md={4}>
                  <Typography variant="caption" color="text.secondary">
                    {t('invoice.invoiceDate')}
                  </Typography>
                  <Typography variant="body1">{formatDate(invoice.invoiceDate)}</Typography>
                </Grid>
                <Grid item xs={6} md={4}>
                  <Typography variant="caption" color="text.secondary">
                    {t('invoice.dueDate')}
                  </Typography>
                  <Typography variant="body1">{invoice.dueDate ? formatDate(invoice.dueDate) : '—'}</Typography>
                </Grid>
                <Grid item xs={6} md={4}>
                  <Typography variant="caption" color="text.secondary">
                    {t('invoice.type')}
                  </Typography>
                  <Typography variant="body1">{t(`typeLabels.${invoice.invoiceType}`)}</Typography>
                </Grid>
                <Grid item xs={6} md={4}>
                  <Typography variant="caption" color="text.secondary">
                    {t('invoice.paymentMethod')}
                  </Typography>
                  <Typography variant="body1">{t(`paymentLabels.${invoice.paymentMethod}`)}</Typography>
                </Grid>
                {invoice.orderReference && (
                  <Grid item xs={6} md={4}>
                    <Typography variant="caption" color="text.secondary">
                      {t('invoice.orderReference')}
                    </Typography>
                    <Typography variant="body1">{invoice.orderReference}</Typography>
                  </Grid>
                )}
              </Grid>
              {invoice.notes && (
                <Box mt={2}>
                  <Typography variant="caption" color="text.secondary">
                    {t('invoice.notes')}
                  </Typography>
                  <Typography variant="body2" whiteSpace="pre-line">
                    {invoice.notes}
                  </Typography>
                </Box>
              )}
            </CardContent>
          </Card>

          <Card sx={{ mb: 3 }}>
            <CardContent sx={{ p: 3 }}>
              <Typography variant="subtitle1" sx={{ fontWeight: 700, mb: 2 }}>
                {t('invoice.lines')}
              </Typography>
              <TableContainer component={Card} sx={{ boxShadow: 'none' }}>
                <Table size="small">
                  <TableHead>
                    <TableRow>
                      <TableCell>{t('common.reference')}</TableCell>
                      <TableCell>{t('common.description')}</TableCell>
                      <TableCell align="right">{t('common.quantity')}</TableCell>
                      <TableCell align="right">{t('common.unitPrice')}</TableCell>
                      <TableCell align="right">{t('invoice.lineTotalHT')}</TableCell>
                    </TableRow>
                  </TableHead>
                  <TableBody>
                    {invoice.lines.map((line) => (
                      <TableRow key={line.id ?? line.reference}>
                        <TableCell>{line.reference}</TableCell>
                        <TableCell>{line.description}</TableCell>
                        <TableCell align="right">{line.quantity}</TableCell>
                        <TableCell align="right" className="tnum" sx={{ whiteSpace: 'nowrap' }}>{formatCurrency(line.unitPriceHT)}</TableCell>
                        <TableCell align="right" className="tnum" sx={{ fontWeight: 600, whiteSpace: 'nowrap' }}>{formatCurrency(line.totalHT)}</TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </TableContainer>
            </CardContent>
          </Card>

          <Card>
            <CardContent sx={{ p: 3 }}>
              <Typography variant="subtitle1" sx={{ fontWeight: 700, mb: 2 }}>
                {t('invoice.payments')}
              </Typography>
              {invoice.payments.length === 0 ? (
                <Box sx={{ py: 3, textAlign: 'center', color: 'text.secondary' }}>
                  <Typography variant="body2">{t('common.none')}</Typography>
                </Box>
              ) : (
                <TableContainer component={Card} sx={{ boxShadow: 'none' }}>
                  <Table size="small">
                    <TableHead>
                      <TableRow>
                        <TableCell>{t('common.date')}</TableCell>
                        <TableCell>{t('invoice.paymentMethod')}</TableCell>
                        <TableCell align="right">{t('common.amount')}</TableCell>
                      </TableRow>
                    </TableHead>
                    <TableBody>
                      {invoice.payments.map((p) => (
                        <TableRow key={p.id}>
                          <TableCell>{formatDate(p.paymentDate)}</TableCell>
                          <TableCell>{t(`paymentLabels.${p.paymentMethod}`)}</TableCell>
                          <TableCell align="right" className="tnum" sx={{ fontWeight: 600 }}>{formatCurrency(p.amount)}</TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                </TableContainer>
              )}
            </CardContent>
          </Card>
        </Grid>

        <Grid item xs={12} lg={4}>
          <Card>
            <CardContent sx={{ p: 3 }}>
              <Typography variant="subtitle1" sx={{ fontWeight: 700, mb: 1.5 }}>
                {t('common.total')}
              </Typography>
              <Box
                sx={{
                  display: 'flex',
                  justifyContent: 'space-between',
                  alignItems: 'center',
                  py: 1.25,
                  px: 1.5,
                  bgcolor: 'primary.light',
                  borderRadius: 2,
                  mb: 1.5,
                }}
              >
                <Typography variant="subtitle1" fontWeight={700} sx={{ color: 'primary.dark' }}>
                  {t('invoice.totalTTC')}
                </Typography>
                <Typography variant="subtitle1" fontWeight={800} sx={{ color: 'primary.dark' }} className="tnum">
                  {formatCurrency(invoice.totalTTC)}
                </Typography>
              </Box>
              <Box sx={{ display: 'flex', justifyContent: 'space-between', py: 0.75 }}>
                <Typography variant="body2" color="text.secondary">{t('invoice.paid')}</Typography>
                <Typography variant="body2" className="tnum" sx={{ color: 'success.main', fontWeight: 700 }}>
                  {formatCurrency(invoice.montantPaye)}
                </Typography>
              </Box>
              <Box sx={{ display: 'flex', justifyContent: 'space-between', py: 0.75 }}>
                <Typography variant="body2" color="text.secondary">{t('invoice.balance')}</Typography>
                <Typography variant="body2" className="tnum" sx={{ fontWeight: 700 }}>
                  {formatCurrency(invoice.soldeRestant)}
                </Typography>
              </Box>
              <Divider sx={{ my: 2 }} />
              <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1 }}>
                {isDraft && (
                  <>
                    <Button variant="contained" onClick={() => navigate(`/invoices/${invoice.id}/edit`)} startIcon={<Pencil size={16} />}>
                      {t('common.edit')}
                    </Button>
                    <Button variant="outlined" onClick={() => void action(() => api.post(`/invoices/${invoice.id}/finalize`))}>
                      {t('invoice.finalize')}
                    </Button>
                    <Button variant="outlined" color="error" onClick={() => void action(() => api.post(`/invoices/${invoice.id}/cancel`))}>
                      {t('invoice.cancelInvoice')}
                    </Button>
                  </>
                )}
                {canPay && (
                  <Button variant="contained" color="success" startIcon={<CreditCard size={16} />} onClick={() => setPayOpen(true)}>
                    {t('invoice.registerPayment')}
                  </Button>
                )}
                {invoice.status !== 'Annulee' && (
                  <Button variant="outlined" startIcon={<Copy size={16} />} onClick={() => void action(() => api.post(`/invoices/${invoice.id}/duplicate`))}>
                    {t('invoice.duplicate')}
                  </Button>
                )}
                {isFacture && (
                  <Button variant="outlined" startIcon={<FilePlus2 size={16} />} onClick={() => void action(() => api.post(`/invoices/${invoice.id}/credit-note`))}>
                    {t('invoice.creditNote')}
                  </Button>
                )}
                <Button
                  variant="outlined"
                  fullWidth
                  startIcon={<Download size={16} />}
                  onClick={() => void download('pdf')}
                  sx={{ mt: 1 }}
                >
                  {t('invoice.exportPdf')}
                </Button>
                <Box sx={{ display: 'flex', gap: 1 }}>
                  <Button variant="outlined" fullWidth onClick={() => void download('xlsx')}>
                    XLSX
                  </Button>
                  <Button variant="outlined" fullWidth onClick={() => void download('docx')}>
                    DOCX
                  </Button>
                </Box>
              </Box>
            </CardContent>
          </Card>
        </Grid>
      </Grid>

      <Dialog open={payOpen} onClose={() => setPayOpen(false)} fullWidth maxWidth="xs">
        <DialogTitle>{t('invoice.registerPayment')}</DialogTitle>
        <DialogContent>
          <TextField
            autoFocus
            margin="dense"
            type="number"
            label={t('common.amount')}
            fullWidth
            value={payAmount}
            onChange={(e) => setPayAmount(e.target.value)}
          />
          <TextField
            select
            margin="dense"
            label={t('invoice.paymentMethod')}
            fullWidth
            value={payMethod}
            onChange={(e) => setPayMethod(e.target.value as PaymentMethod)}
          >
            {(['Comptant', 'Cheque', 'VirementBancaire', 'CarteBancaire', 'Credit'] as PaymentMethod[]).map((pm) => (
              <MenuItem key={pm} value={pm}>
                {t(`paymentLabels.${pm}`)}
              </MenuItem>
            ))}
          </TextField>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setPayOpen(false)}>{t('common.cancel')}</Button>
          <Button variant="contained" onClick={() => void submitPayment()}>
            {t('common.confirm')}
          </Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
}
