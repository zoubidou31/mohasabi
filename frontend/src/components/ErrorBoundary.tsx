import { Component, type ErrorInfo, type ReactNode } from 'react';
import { Box, Button, Collapse, Dialog, Typography } from '@mui/material';
import { ShieldAlert } from 'lucide-react';
import { withTranslation, type WithTranslation } from 'react-i18next';
import { useSettingsStore } from '../stores/settingsStore';

interface Props extends WithTranslation {
  children: ReactNode;
}

interface State {
  error: Error | null;
  message: string | null;
  stack: string | null;
  showDetails: boolean;
  restarting: boolean;
}

class ErrorBoundaryInner extends Component<Props, State> {
  state: State = {
    error: null,
    message: null,
    stack: null,
    showDetails: false,
    restarting: false,
  };

  constructor(props: Props) {
    super(props);
    this.onGlobalError = this.onGlobalError.bind(this);
    this.onUnhandledRejection = this.onUnhandledRejection.bind(this);
  }

  static getDerivedStateFromError(error: Error): Partial<State> {
    return { error, message: error.message || 'Erreur inconnue', stack: error.stack || null };
  }

  componentDidMount() {
    window.addEventListener('error', this.onGlobalError);
    window.addEventListener('unhandledrejection', this.onUnhandledRejection);
  }

  componentWillUnmount() {
    window.removeEventListener('error', this.onGlobalError);
    window.removeEventListener('unhandledrejection', this.onUnhandledRejection);
  }

  componentDidCatch(_error: Error, errorInfo: ErrorInfo) {
    // Les détails techniques restent masqués par défaut (aucune fuite en interface).
    console.error('[Crash]', this.state.stack, errorInfo);
  }

  private onGlobalError(event: ErrorEvent) {
    this.report(event.message, event.error?.stack ?? null);
  }

  private onUnhandledRejection(event: PromiseRejectionEvent) {
    const reason: unknown = event.reason;
    this.report(
      reason instanceof Error ? reason.message : String(reason ?? 'Rejet non géré'),
      reason instanceof Error ? (reason.stack ?? null) : null,
    );
  }

  private report(message: string, stack: string | null) {
    if (this.state.error) return;
    this.setState({ error: new Error(message), message, stack, restarting: false });
  }

  private handleRestart = () => {
    if (this.state.restarting) return;
    this.setState({ restarting: true });
    void useSettingsStore.getState().restartApp().finally(() => {
      // Si la demande n'a pas provoqué le redémarrage, laisser l'utilisateur réessayer.
      window.setTimeout(() => this.setState({ restarting: false }), 2500);
    });
  };

  private handleClose = () => {
    // « Fermer » ne ferme que le dialogue ; l'application reste ouverte mais
    // dégradée, afin de ne jamais bloquer l'accès aux données.
    this.setState({ error: null });
  };

  render() {
    const { t } = this.props;
    if (this.state.error) {
      return (
        <Dialog open fullScreen>
          <Box
            sx={{
              height: '100%',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              px: 2,
              backgroundColor: 'background.default',
            }}
          >
            <Box sx={{ maxWidth: 520, width: '100%', textAlign: 'center' }}>
              <Box
                sx={{
                  width: 72,
                  height: 72,
                  mx: 'auto',
                  mb: 2,
                  borderRadius: 3,
                  display: 'grid',
                  placeItems: 'center',
                  backgroundColor: 'error.light',
                  color: 'error.main',
                }}
              >
                <ShieldAlert size={34} />
              </Box>
              <Typography variant="h5" fontWeight={800} gutterBottom>
                {t('crash.title')}
              </Typography>
              <Typography sx={{ color: 'text.secondary', mb: 3 }}>{t('crash.body')}</Typography>

              <Box sx={{ display: 'flex', justifyContent: 'center', gap: 1.5, mb: 2 }}>
                <Button
                  variant="contained"
                  color="error"
                  onClick={this.handleRestart}
                  disabled={this.state.restarting}
                >
                  {this.state.restarting ? t('crash.restarting') : t('crash.restart')}
                </Button>
                <Button variant="outlined" onClick={this.handleClose}>
                  {t('crash.close')}
                </Button>
              </Box>

              <Box sx={{ display: 'flex', justifyContent: 'center' }}>
                <Button
                  size="small"
                  sx={{ color: 'text.secondary', fontSize: 12 }}
                  onClick={() => this.setState((s) => ({ showDetails: !s.showDetails }))}
                >
                  {this.state.showDetails ? `▲ ${t('crash.details')}` : `▼ ${t('crash.details')}`}
                </Button>
              </Box>
              <Collapse in={this.state.showDetails}>
                <Box
                  sx={{
                    mt: 1.5,
                    p: 1.5,
                    borderRadius: 2,
                    backgroundColor: 'grey.900',
                    color: '#E5E7EB',
                    fontSize: 12,
                    textAlign: 'left',
                    maxHeight: 200,
                    overflow: 'auto',
                    fontFamily: 'monospace',
                    whiteSpace: 'pre-wrap',
                    wordBreak: 'break-word',
                  }}
                >
                  {this.state.message}
                  {this.state.stack ? `\n\n${this.state.stack}` : ''}
                </Box>
                <Typography sx={{ fontSize: 11, color: 'text.secondary', mt: 1 }}>
                  {t('crash.detailsHint')}
                </Typography>
              </Collapse>
            </Box>
          </Box>
        </Dialog>
      );
    }

    return this.props.children;
  }
}

export const ErrorBoundary = withTranslation()(ErrorBoundaryInner);
