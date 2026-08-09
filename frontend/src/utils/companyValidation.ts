export interface ValidationErrors {
  [key: string]: string;
}

function safeStr(value: unknown): string {
  return typeof value === 'string' ? value : '';
}

// ──────────────────────────────────────────────────────────────
// Détection opérateur mobile (pour affichage badge uniquement)
// ──────────────────────────────────────────────────────────────
export type MobileOperator = 'Ooredoo' | 'Mobilis' | 'Djezzy' | '';

export function detectMobileOperator(phone: unknown): MobileOperator {
  const v = safeStr(phone).replace(/\s/g, '');
  if (/^0?5/.test(v)) return 'Ooredoo';
  if (/^0?6/.test(v)) return 'Mobilis';
  if (/^0?7/.test(v)) return 'Djezzy';
  return '';
}

// ──────────────────────────────────────────────────────────────
// NIF : 15 chiffres obligatoires (norme algérienne)
// ──────────────────────────────────────────────────────────────
export function validateNIF(value: unknown): string {
  const v = safeStr(value).trim();
  if (!v) return 'Le NIF est obligatoire.';
  if (!/^\d+$/.test(v)) return 'Le NIF ne doit contenir que des chiffres.';
  if (v.length !== 15) return `Le NIF doit contenir exactement 15 chiffres (${v.length}/15).`;
  return '';
}

// ──────────────────────────────────────────────────────────────
// NIS : 15 chiffres obligatoires (norme algérienne)
// ──────────────────────────────────────────────────────────────
export function validateNIS(value: unknown): string {
  const v = safeStr(value).trim();
  if (!v) return 'Le NIS est obligatoire.';
  if (!/^\d+$/.test(v)) return 'Le NIS ne doit contenir que des chiffres.';
  if (v.length !== 15) return `Le NIS doit contenir exactement 15 chiffres (${v.length}/15).`;
  return '';
}

// ──────────────────────────────────────────────────────────────
// RC : format 16/00-0000000B00 (chiffres, lettres majuscules, / et -)
// ──────────────────────────────────────────────────────────────
export function validateRC(value: unknown): string {
  const v = safeStr(value).trim();
  if (!v) return 'Le RC est obligatoire.';
  if (!/^[A-Z0-9/\-]+$/.test(v)) return 'Le RC ne doit contenir que des chiffres, lettres majuscules, / et -.';
  if (!/^\d{2}\/\d{2}-\d{7}[A-Z]\d{2}$/.test(v)) return 'Format RC attendu : 16/00-0000000B00.';
  return '';
}

// ──────────────────────────────────────────────────────────────
// ART : 13 chiffres obligatoires
// ──────────────────────────────────────────────────────────────
export function validateART(value: unknown): string {
  const v = safeStr(value).trim();
  if (!v) return 'Le ART est obligatoire.';
  if (!/^\d+$/.test(v)) return 'Le ART ne doit contenir que des chiffres.';
  if (v.length !== 13) return `Le ART doit contenir exactement 13 chiffres (${v.length}/13).`;
  return '';
}

// ──────────────────────────────────────────────────────────────
// Téléphone algérien : 05/06/07 + 8 chiffres = 10 total
// ──────────────────────────────────────────────────────────────
const PHONE_REGEX = /^(?:0[567]\d{8}|\d{9})$/;

export function validatePhone(value: unknown, required = true): string {
  const v = safeStr(value).trim().replace(/\s/g, '');
  if (!v) return required ? 'Le téléphone est obligatoire.' : '';
  if (!/^\d+$/.test(v)) return 'Le téléphone ne doit contenir que des chiffres.';
  if (v.length !== 10 && v.length !== 9) return 'Le téléphone doit contenir 10 chiffres (0550XXXXXX) ou 9 chiffres (550XXXXXX).';
  if (!PHONE_REGEX.test(v)) return 'Le téléphone doit commencer par 05, 06 ou 07 + 8 chiffres.';
  return '';
}

// ──────────────────────────────────────────────────────────────
// Email : @ + domaine valide + extension .com/.dz/.net/.org
// ──────────────────────────────────────────────────────────────
export function validateEmail(value: unknown, required = true): string {
  const v = safeStr(value).trim();
  if (!v) return required ? "L'e-mail est obligatoire." : '';
  if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(v)) return "Format d'e-mail invalide.";
  if (!/\.(com|dz|net|org)$/i.test(v)) return "Le domaine doit se terminer par .com, .dz, .net ou .org.";
  return '';
}

// ──────────────────────────────────────────────────────────────
// Adresse : min 10 caractères, pas de caractères bizarres
// ──────────────────────────────────────────────────────────────
export function validateAddress(value: unknown): string {
  const v = safeStr(value).trim();
  if (!v) return "L'adresse est obligatoire.";
  if (v.length < 10) return `L'adresse doit contenir au moins 10 caractères (${v.length}/10).`;
  if (/[<>{}`~\\|]/.test(v)) return "L'adresse contient des caractères non autorisés.";
  return '';
}

// ──────────────────────────────────────────────────────────────
// Code postal : 5 chiffres, premier 2 = code wilaya
// ──────────────────────────────────────────────────────────────
export function validatePostalCode(value: unknown, wilaya?: unknown): string {
  const v = safeStr(value).trim();
  const w = safeStr(wilaya).trim();
  if (w && !v) return 'Le code postal est obligatoire quando une wilaya est sélectionnée.';
  if (!v) return '';
  if (!/^\d+$/.test(v)) return 'Le code postal ne doit contenir que des chiffres.';
  if (v.length !== 5) return `Le code postal doit contenir exactement 5 chiffres (${v.length}/5).`;
  if (w && v.slice(0, 2) !== w) return `Le code postal doit commencer par ${w} (code wilaya).`;
  return '';
}

// ──────────────────────────────────────────────────────────────
// RIB : 20 chiffres obligatoires
// ──────────────────────────────────────────────────────────────
export function validateRIB(value: unknown): string {
  const v = safeStr(value).trim();
  if (!v) return ''; // optionnel
  if (!/^\d+$/.test(v)) return 'Le RIB ne doit contenir que des chiffres.';
  if (v.length !== 20) return `Le RIB doit contenir exactement 20 chiffres (${v.length}/20).`;
  return '';
}

// ──────────────────────────────────────────────────────────────
// CCP : 6 à 12 chiffres
// ──────────────────────────────────────────────────────────────
export function validateCCP(value: unknown): string {
  const v = safeStr(value).trim();
  if (!v) return ''; // optionnel
  if (!/^\d+$/.test(v)) return 'Le CCP ne doit contenir que des chiffres.';
  if (v.length < 6 || v.length > 12) return 'Le CCP doit contenir entre 6 et 12 chiffres.';
  return '';
}

// ──────────────────────────────────────────────────────────────
// Raison sociale : obligatoire, min 2 caractères
// ──────────────────────────────────────────────────────────────
export function validateCompanyName(value: unknown): string {
  const v = safeStr(value).trim();
  if (!v) return 'La raison sociale est obligatoire.';
  if (v.length < 2) return 'La raison sociale doit contenir au moins 2 caractères.';
  return '';
}

// ──────────────────────────────────────────────────────────────
// Préfixe facturation : obligatoire, majuscules/chiffres/tirets
// ──────────────────────────────────────────────────────────────
export function validateInvoicePrefix(value: unknown): string {
  const v = safeStr(value).trim();
  if (!v) return 'Le préfixe de facturation est obligatoire.';
  if (!/^[A-Z0-9\-]+$/.test(v)) return 'Le préfixe ne doit contenir que des lettres majuscules, chiffres et tirets.';
  return '';
}

// ──────────────────────────────────────────────────────────────
// Validité (jours) : 0-365
// ──────────────────────────────────────────────────────────────
export function validateValidityDays(value: unknown): string {
  const n = typeof value === 'number' ? value : parseInt(safeStr(value), 10);
  if (isNaN(n) || n < 0) return 'La validité ne peut pas être négative.';
  if (n > 365) return 'La validité ne peut pas dépasser 365 jours.';
  return '';
}

// ──────────────────────────────────────────────────────────────
// Conditions de paiement : obligatoire, non vide
// ──────────────────────────────────────────────────────────────
export function validatePaymentConditions(value: unknown): string {
  const v = safeStr(value).trim();
  if (!v) return 'Les conditions de paiement sont obligatoires.';
  return '';
}

// ──────────────────────────────────────────────────────────────
// Pénalités : optionnel, doit contenir un pourcentage (ex: 0.5%)
// ──────────────────────────────────────────────────────────────
export function validatePenalties(value: unknown): string {
  const v = safeStr(value).trim();
  if (!v) return ''; // optionnel
  // Match percentage anywhere: 0.5%, 0,5%, 1%, 10%, etc.
  if (!/\d+(?:[.,]\d+)?\s*%/.test(v)) return 'Les pénalités doivent contenir un pourcentage (ex : 0.5% par mois).';
  return '';
}

// ──────────────────────────────────────────────────────────────
// Référence produit : requise, alphanumérique + tirets/underscores
// ──────────────────────────────────────────────────────────────
export function validateProductReference(value: unknown): string {
  const v = safeStr(value).trim();
  if (!v) return 'La référence est obligatoire.';
  if (v.length > 50) return 'La référence ne doit pas dépasser 50 caractères.';
  return '';
}

// ──────────────────────────────────────────────────────────────
// Nom / Désignation produit
// ──────────────────────────────────────────────────────────────
export function validateProductName(value: unknown): string {
  const v = safeStr(value).trim();
  if (!v) return 'La désignation est obligatoire.';
  if (v.length < 2) return 'La désignation doit contenir au moins 2 caractères.';
  return '';
}

// ──────────────────────────────────────────────────────────────
// Prix HT
// ──────────────────────────────────────────────────────────────
export function validateProductPrice(value: unknown): string {
  const n = typeof value === 'number' ? value : parseFloat(safeStr(value));
  if (isNaN(n) || n < 0) return 'Le prix HT ne peut pas être négatif.';
  return '';
}

// ──────────────────────────────────────────────────────────────
// Validation complète formulaire entreprise
// ──────────────────────────────────────────────────────────────
export interface CompanyForm {
  companyName: string;
  nif: string;
  nis: string;
  rc: string;
  art: string;
  address: string;
  city: string;
  wilaya: string;
  postalCode: string;
  phone: string;
  mobile: string;
  email: string;
  rib: string;
  ccp: string;
  bankName: string;
  invoicePrefix: string;
  validityDays: number;
  paymentConditions: string;
  penalties: string;
}

export function validateCompanyForm(form: CompanyForm): ValidationErrors {
  const errors: ValidationErrors = {};

  const nif = validateNIF(form.nif); if (nif) errors.nif = nif;
  const nis = validateNIS(form.nis); if (nis) errors.nis = nis;
  const rc = validateRC(form.rc); if (rc) errors.rc = rc;
  const art = validateART(form.art); if (art) errors.art = art;
  const phone = validatePhone(form.phone); if (phone) errors.phone = phone;
  const mobile = validatePhone(form.mobile, false); if (mobile) errors.mobile = mobile;
  const email = validateEmail(form.email); if (email) errors.email = email;
  const address = validateAddress(form.address); if (address) errors.address = address;
  const postalCode = validatePostalCode(form.postalCode, form.wilaya); if (postalCode) errors.postalCode = postalCode;
  const rib = validateRIB(form.rib); if (rib) errors.rib = rib;
  const ccp = validateCCP(form.ccp); if (ccp) errors.ccp = ccp;
  const companyName = validateCompanyName(form.companyName); if (companyName) errors.companyName = companyName;
  const invoicePrefix = validateInvoicePrefix(form.invoicePrefix); if (invoicePrefix) errors.invoicePrefix = invoicePrefix;
  const validityDays = validateValidityDays(form.validityDays); if (validityDays) errors.validityDays = validityDays;
  const paymentConditions = validatePaymentConditions(form.paymentConditions); if (paymentConditions) errors.paymentConditions = paymentConditions;
  const penalties = validatePenalties(form.penalties); if (penalties) errors.penalties = penalties;

  return errors;
}
