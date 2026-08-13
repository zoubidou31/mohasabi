import { Box, Paper, Typography } from '@mui/material';
import { useTranslation } from 'react-i18next';

interface InvoicePreviewProps {
  fontFamily: string;
  baseFontSize: number;
  tableFontSize: number;
  headerFontSize: number;
  footerFontSize: number;
}

const sampleLines = [
  { ref: 'ART-001', desc: 'Prestation de conseil et développement', qty: 2, pu: 1500, ht: 3000 },
  { ref: 'ART-002', desc: 'Licence logicielle annuelle', qty: 1, pu: 2400, ht: 2400 },
  { ref: 'ART-003', desc: 'Formation utilisateurs (jour)', qty: 3, pu: 900, ht: 2700 },
];

/** Aperçu fidèle d'une facture utilisant la typographie document choisie. */
export default function InvoicePreview({
  fontFamily,
  baseFontSize,
  tableFontSize,
  headerFontSize,
  footerFontSize,
}: InvoicePreviewProps) {
  const { t } = useTranslation();
  const family = `'${fontFamily}', sans-serif`;
  const fmt = (n: number) => `${n.toLocaleString('fr-FR')} DA`;

  return (
    <Paper
      elevation={0}
      variant="outlined"
      sx={{
        p: 2,
        fontFamily: family,
        color: '#1f2937',
        background: '#fff',
        overflow: 'hidden',
        userSelect: 'none',
      }}
    >
      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', mb: 1 }}>
        <Box>
          <Typography sx={{ fontSize: headerFontSize + 4, fontWeight: 800, lineHeight: 1.1 }}>
            SARL Mohasabi
          </Typography>
          <Typography sx={{ fontSize: footerFontSize + 1, color: '#6b7280' }}>
            12 Rue des Oliviers, Alger · NIF 000000000000000
          </Typography>
        </Box>
        <Box sx={{ textAlign: 'right' }}>
          <Typography sx={{ fontSize: headerFontSize, fontWeight: 700 }}>FACTURE</Typography>
          <Typography sx={{ fontSize: footerFontSize + 1, color: '#6b7280' }}>FA-2026-0042</Typography>
        </Box>
      </Box>

      <Box sx={{ fontSize: baseFontSize, mb: 1 }}>
        <strong>{t('options.typography.previewTitle')}</strong> · Client SARL Example
      </Box>

      <Box
        component="table"
        sx={{
          width: '100%',
          borderCollapse: 'collapse',
          fontSize: tableFontSize,
          '& th, & td': { borderBottom: '1px solid #e5e7eb', textAlign: 'left', px: 0.5, py: 0.25 },
          '& th': { fontWeight: 700, color: '#374151' },
        }}
      >
        <thead>
          <tr>
            <th>Réf.</th>
            <th>Désignation</th>
            <th>Qté</th>
            <th>PU</th>
            <th>HT</th>
          </tr>
        </thead>
        <tbody>
          {sampleLines.map((l) => (
            <tr key={l.ref}>
              <td>{l.ref}</td>
              <td>{l.desc}</td>
              <td>{l.qty}</td>
              <td>{fmt(l.pu)}</td>
              <td>{fmt(l.ht)}</td>
            </tr>
          ))}
        </tbody>
      </Box>

      <Box sx={{ display: 'flex', justifyContent: 'flex-end', gap: 2, mt: 1, fontSize: baseFontSize }}>
        <Box>
          <div>Total HT</div>
          <div>Total TVA</div>
          <div style={{ fontWeight: 800 }}>Total TTC</div>
        </Box>
        <Box sx={{ textAlign: 'right', fontVariantNumeric: 'tabular-nums' }}>
          <div>{fmt(8100)}</div>
          <div>{fmt(1458)}</div>
          <div style={{ fontWeight: 800 }}>{fmt(9558)}</div>
        </Box>
      </Box>

      <Box
        sx={{
          mt: 1,
          pt: 0.5,
          borderTop: '1px solid #e5e7eb',
          fontSize: footerFontSize,
          color: '#9ca3af',
        }}
      >
        Merci de votre confiance — règlement à 30 jours.
      </Box>
    </Paper>
  );
}
