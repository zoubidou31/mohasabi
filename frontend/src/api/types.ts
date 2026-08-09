export type ClientType = 'Entreprise' | 'Particulier' | 'ProfessionnelLiberal';
export type InvoiceType = 'Facture' | 'ProForma' | 'Avoir';
export type InvoiceStatus = 'Brouillon' | 'Finalisee' | 'Payee' | 'Annulee';
export type PaymentMethod = 'Comptant' | 'Cheque' | 'VirementBancaire' | 'CarteBancaire' | 'Credit';
export type TVARate = 'Normal' | 'Reduit' | 'Exonere' | 'IFU';

export interface Client {
  id: string;
  displayName: string;
  companyName?: string;
  sector?: string;
  nif?: string;
  rc?: string;
  art?: string;
  address: string;
  postalCode?: string;
  city?: string;
  wilaya?: string;
  phone: string;
  mobile?: string;
  email?: string;
  type: ClientType;
  defaultPaymentMethod?: PaymentMethod;
  notes?: string;
  createdDate: string;
  invoiceCount: number;
  totalSpent: number;
  lastInvoiceDate?: string;
}

export interface ClientStats {
  clientId: string;
  invoiceCount: number;
  totalSpent: number;
  totalPaid: number;
  outstanding: number;
  lastInvoiceDate?: string;
  recentInvoices: InvoiceSummary[];
}

export interface Product {
  id: string;
  reference: string;
  name: string;
  description?: string;
  category?: string;
  categoryId?: string;
  categoryName?: string;
  defaultPrice: number;
  defaultTvaRate: TVARate;
  isService: boolean;
  isActive: boolean;
}

export interface Category {
  id: string;
  name: string;
  description?: string;
  isActive: boolean;
}

export interface InvoiceLine {
  id?: string;
  productId?: string;
  reference: string;
  description: string;
  quantity: number;
  unitPriceHT: number;
  tvaRate: TVARate;
  totalHT: number;
  tvaAmount: number;
  totalTTC: number;
  sortOrder: number;
}

export interface TVABreakdown {
  tvaRate: TVARate;
  label: string;
  totalHT: number;
  tvaAmount: number;
  totalTTC: number;
}

export interface Payment {
  id: string;
  paymentDate: string;
  amount: number;
  paymentMethod: PaymentMethod;
  chequeNumber?: string;
  notes?: string;
}

export interface InvoiceSummary {
  id: string;
  invoiceNumber: string;
  clientName: string;
  invoiceDate: string;
  dueDate?: string;
  invoiceType: InvoiceType;
  status: InvoiceStatus;
  totalHT: number;
  totalTVA: number;
  totalTTC: number;
  montantPaye: number;
  soldeRestant: number;
  isOverdue: boolean;
}

export interface Invoice extends InvoiceSummary {
  clientId: string;
  companyId: string;
  sequence: number;
  paymentMethod: PaymentMethod;
  chequeNumber?: string;
  orderReference?: string;
  bonCommande?: string;
  notes?: string;
  mentionsSpecifiques?: string;
  paymentConditions?: string;
  penalties?: string;
  validityDays: number;
  remiseValue?: number;
  remiseIsPercentage: boolean;
  remiseAmount: number;
  fraisPort?: number;
  fraisPortLabel?: string;
  autresFrais?: number;
  autresFraisLabel?: string;
  creditNoteForInvoiceId?: string;
  createdBy?: string;
  createdDate: string;
  finalizedDate?: string;
  paidDate?: string;
  cancelledDate?: string;
  client?: Client;
  lines: InvoiceLine[];
  tvaBreakdowns: TVABreakdown[];
  payments: Payment[];
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export interface Company {
  id: string;
  companyName: string;
  logoPath?: string;
  address: string;
  postalCode?: string;
  city?: string;
  wilaya?: string;
  phone: string;
  mobile?: string;
  email: string;
  nif: string;
  nis: string;
  rc: string;
  art: string;
  rib?: string;
  ccp?: string;
  bankName?: string;
  invoicePrefix: string;
  invoiceSerie: string;
  validityDays: number;
  defaultTvaRate: TVARate;
  paymentConditions?: string;
  penalties?: string;
  bankAccountNumber?: string;
  stampPath?: string;
  useBankersRounding: boolean;
}

export interface MonthlyReport {
  year: number;
  month: number;
  invoiceCount: number;
  totalHT: number;
  totalTVA: number;
  totalTTC: number;
  totalCollected: number;
  outstanding: number;
  tvaByRate: TVAReport[];
  invoices: InvoiceSummary[];
}

export interface TVAReport {
  tvaRate: string;
  totalHT: number;
  tvaAmount: number;
  totalTTC: number;
}

export interface AuditLog {
  id: string;
  userId?: string;
  userName?: string;
  entityType: string;
  entityId: string;
  action: string;
  changedData?: string;
  timestamp: string;
}

export interface CreateInvoiceRequest {
  clientId: string;
  invoiceDate: string;
  validityDays: number;
  invoiceType: InvoiceType;
  paymentMethod: PaymentMethod;
  chequeNumber?: string;
  orderReference?: string;
  bonCommande?: string;
  notes?: string;
  mentionsSpecifiques?: string;
  paymentConditions?: string;
  penalties?: string;
  remiseValue?: number;
  remiseIsPercentage: boolean;
  fraisPort?: number;
  fraisPortLabel?: string;
  autresFrais?: number;
  autresFraisLabel?: string;
  lines: {
    productId?: string;
    reference: string;
    description: string;
    quantity: number;
    unitPriceHT: number;
    tvaRate: TVARate;
  }[];
}
