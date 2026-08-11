import { TablePagination } from '@mui/material';
import { useTranslation } from 'react-i18next';

interface TablePaginationBarProps {
  count: number;
  page: number;
  rowsPerPage: number;
  onPageChange: (page: number) => void;
  onRowsPerPageChange: (rowsPerPage: number) => void;
  rowsPerPageOptions?: number[];
}

export default function TablePaginationBar({
  count,
  page,
  rowsPerPage,
  onPageChange,
  onRowsPerPageChange,
  rowsPerPageOptions = [10, 20, 50, 100],
}: TablePaginationBarProps) {
  const { t } = useTranslation();
  return (
    <TablePagination
      component="div"
      count={count}
      page={page}
      onPageChange={(_, p) => onPageChange(p)}
      rowsPerPage={rowsPerPage}
      onRowsPerPageChange={(e) => onRowsPerPageChange(parseInt(e.target.value, 10))}
      labelRowsPerPage={t('common.rowsPerPage')}
      rowsPerPageOptions={rowsPerPageOptions}
      sx={{ borderTop: '1px solid', borderColor: 'divider' }}
    />
  );
}
