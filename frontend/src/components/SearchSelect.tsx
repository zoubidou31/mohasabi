import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { Autocomplete, CircularProgress, TextField } from '@mui/material';
import { api } from '../api/client';
import type { PagedResult } from '../api/types';

interface Props<T> {
  endpoint: string;
  params?: Record<string, string | number | boolean>;
  value: T | null;
  onChange: (value: T | null) => void;
  getOptionLabel: (option: T) => string;
  label: string;
  placeholder?: string;
  size?: 'small' | 'medium';
  disabled?: boolean;
  fullWidth?: boolean;
}

export default function SearchSelect<T extends { id: string }>({
  endpoint,
  params,
  value,
  onChange,
  getOptionLabel,
  label,
  placeholder,
  size = 'medium',
  disabled,
  fullWidth,
}: Props<T>) {
  const [options, setOptions] = useState<T[]>([]);
  const [loading, setLoading] = useState(false);
  const [input, setInput] = useState('');
  const timer = useRef<number | undefined>(undefined);

  // On ne dépend pas de l'identité de `params` : on lit la dernière valeur via une ref.
  const requestRef = useRef({ endpoint, params });
  requestRef.current = { endpoint, params };

  const fetchOptions = useCallback(async (term: string) => {
    const { endpoint: ep, params: p } = requestRef.current;
    setLoading(true);
    try {
      const query = { search: term || undefined, page: 1, pageSize: 50, ...p };
      const { data } = await api.get<PagedResult<T>>(ep, { params: query });
      setOptions(data.items);
    } catch {
      setOptions([]);
    } finally {
      setLoading(false);
    }
  }, []);

  const handleOpen = () => {
    void fetchOptions(input);
  };

  useEffect(() => {
    window.clearTimeout(timer.current);
    timer.current = window.setTimeout(() => {
      void fetchOptions(input);
    }, 250);
    return () => window.clearTimeout(timer.current);
  }, [input, fetchOptions]);

  const displayOptions = useMemo(() => {
    if (value && !options.some((o) => o.id === value.id)) {
      return [value, ...options];
    }
    return options;
  }, [options, value]);

  return (
    <Autocomplete<T, false, false, false>
      options={displayOptions}
      value={value}
      onChange={(_e, val) => onChange(val)}
      onInputChange={(_e, val) => setInput(val)}
      onOpen={handleOpen}
      getOptionLabel={(option) => getOptionLabel(option)}
      isOptionEqualToValue={(a, b) => a.id === b.id}
      filterOptions={(opts) => opts}
      loading={loading}
      disabled={disabled}
      size={size}
      fullWidth={fullWidth}
      noOptionsText="—"
      renderInput={(params) => (
        <TextField
          {...params}
          label={label}
          placeholder={placeholder}
          size={size}
          InputProps={{
            ...params.InputProps,
            endAdornment: (
              <>
                {loading ? <CircularProgress color="inherit" size={18} /> : null}
                {params.InputProps.endAdornment}
              </>
            ),
          }}
        />
      )}
    />
  );
}
