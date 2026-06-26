interface LineItem {
  id: string;
  productId: string;
  sku: string;
  name: string;
  quantity: number;
  unitPrice: number;
  lineTotal: number;
  stockStatus: "InStock" | "AlternativeSuggested" | "BackOrder";
}

interface InvoicePreviewProps {
  invoice: {
    id: string;
    status: string;
    customer: { name: string; type: string };
    lineItems: LineItem[];
    subtotal: number;
    discountPercentage: number;
    discountAmount: number;
    taxPercentage: number;
    taxAmount: number;
    total: number;
  };
}

const STOCK_BADGE: Record<string, string> = {
  InStock: "bg-success/20 text-green-400",
  AlternativeSuggested: "bg-warning/20 text-yellow-400",
  BackOrder: "bg-error/20 text-red-400",
};

const STOCK_LABEL: Record<string, string> = {
  InStock: "In Stock",
  AlternativeSuggested: "Alt. Suggested",
  BackOrder: "Back Order",
};

function fmt(n: number) {
  return n.toLocaleString("en-US", { style: "currency", currency: "USD" });
}

export function InvoicePreview({ invoice }: InvoicePreviewProps) {
  return (
    <section aria-label="Invoice preview" className="bg-surface border border-muted/40 rounded-xl p-4">
      <div className="flex items-center justify-between mb-4">
        <div>
          <h2 className="font-semibold text-foreground">{invoice.customer.name}</h2>
          <p className="text-xs text-foreground-muted">{invoice.customer.type}</p>
        </div>
        <span className={`text-xs px-2 py-1 rounded-full font-medium ${
          invoice.status === "Finalised" ? "bg-success/20 text-green-400" : "bg-primary/20 text-primary-light"
        }`}>
          {invoice.status}
        </span>
      </div>

      <div className="overflow-x-auto">
        <table className="w-full text-sm" aria-label="Invoice line items">
          <thead>
            <tr className="text-foreground-muted text-xs uppercase border-b border-muted/40">
              <th className="text-left py-2 pr-3">Product</th>
              <th className="text-right py-2 pr-3">Qty</th>
              <th className="text-right py-2 pr-3">Unit Price</th>
              <th className="text-right py-2 pr-3">Total</th>
              <th className="text-left py-2">Stock</th>
            </tr>
          </thead>
          <tbody>
            {invoice.lineItems.map((line) => (
              <tr key={line.id} className="border-b border-muted/20">
                <td className="py-2 pr-3">
                  <span className="font-medium">{line.name}</span>
                  <span className="block text-xs text-foreground-muted">{line.sku}</span>
                </td>
                <td className="text-right py-2 pr-3">{line.quantity}</td>
                <td className="text-right py-2 pr-3">{fmt(line.unitPrice)}</td>
                <td className="text-right py-2 pr-3">{fmt(line.lineTotal)}</td>
                <td className="py-2">
                  <span className={`text-xs px-2 py-0.5 rounded-full ${STOCK_BADGE[line.stockStatus] ?? ""}`}>
                    {STOCK_LABEL[line.stockStatus] ?? line.stockStatus}
                  </span>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <div className="mt-4 text-sm space-y-1 text-right">
        <div className="flex justify-between text-foreground-muted">
          <span>Subtotal</span><span>{fmt(invoice.subtotal)}</span>
        </div>
        {invoice.discountAmount > 0 && (
          <div className="flex justify-between text-foreground-muted">
            <span>Discount ({invoice.discountPercentage}%)</span>
            <span>−{fmt(invoice.discountAmount)}</span>
          </div>
        )}
        <div className="flex justify-between text-foreground-muted">
          <span>Tax ({invoice.taxPercentage}%)</span>
          <span>{fmt(invoice.taxAmount)}</span>
        </div>
        <div className="flex justify-between font-semibold text-foreground border-t border-muted/40 pt-2">
          <span>Total</span><span>{fmt(invoice.total)}</span>
        </div>
      </div>
    </section>
  );
}
