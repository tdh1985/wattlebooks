// Copyright (c) 2026 Tim Downey. Licensed under the MIT License.

namespace InvoiceDesk.Core.Domain;

public enum InvoiceStatus { Draft = 0, Sent = 1, Void = 2, Accepted = 3, Declined = 4 }

// quotes share the invoice table so the editor and pdf carry over
public enum InvoiceKind { Invoice = 0, Quote = 1 }

// never stored, worked out from payments and due date so it can't go stale
public enum DisplayStatus { Draft, Sent, PartPaid, Paid, Overdue, Void, Accepted, Declined, Expired }

public enum Direction { In = 0, Out = 1 }

public enum PaymentMethod { None = 0, BankTransfer = 1, Card = 2, Cash = 3, Other = 4 }

public enum AttachmentKind { Receipt = 0, SentInvoicePdf = 1, Logo = 2, Other = 3 }

public enum ReminderTone { Polite = 0, Firm = 1, Final = 2 }

public enum RepeatEvery { Weekly = 0, Fortnightly = 1, Monthly = 2, Quarterly = 3, Yearly = 4 }
