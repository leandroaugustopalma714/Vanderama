const $ = selector => document.querySelector(selector);
const currency = new Intl.NumberFormat("pt-BR", { style: "currency", currency: "BRL" });
const state = { orders: [], customers: [], products: [] };
const targets = { orders: $("#orders-body"), customers: $("#customers-body"), products: $("#products-body") };

function cell(text, className = "") { const node = document.createElement("td"); node.textContent = text ?? "—"; if (className) node.className = className; return node; }
function empty(target, message, columns) { const row = document.createElement("tr"); const td = cell(message, "empty"); td.colSpan = columns; row.append(td); target.replaceChildren(row); }
function showMessage(text, error = false) { const node = $("#message"); node.textContent = text; node.className = `message visible ${error ? "error" : "success"}`; window.setTimeout(() => node.classList.remove("visible"), 4500); }
function setConnection(online) { $("#connection-dot").className = `dot ${online ? "online" : "offline"}`; $("#connection-label").textContent = online ? "API conectada" : "Sem conexão"; }
function date(value) { return new Intl.DateTimeFormat("pt-BR", { dateStyle: "short", timeStyle: "short" }).format(new Date(value)); }

function renderOrders(rows) {
  if (!rows.length) return empty(targets.orders, "Nenhum pedido recebido ainda.", 6);
  targets.orders.replaceChildren();
  for (const order of rows) {
    const row = document.createElement("tr");
    row.append(cell(`#${String(order.id).padStart(5, "0")}`, "strong"), cell(order.clienteNome, "strong"), cell(date(order.criadoEmUtc)));
    const items = document.createElement("td");
    const details = document.createElement("details"); const summary = document.createElement("summary"); summary.textContent = `${order.itens.length} produto(s)`; details.append(summary);
    const list = document.createElement("ul");
    for (const item of order.itens) { const li = document.createElement("li"); li.textContent = `${item.quantidade} ${item.unidadeMedida} — ${item.nomeProduto} (${currency.format(item.subtotal)})`; list.append(li); }
    details.append(list); items.append(details); row.append(items, cell(order.status, "badge"), cell(currency.format(order.valorTotal), "number strong")); targets.orders.append(row);
  }
}

function renderCustomers(rows) {
  if (!rows.length) return empty(targets.customers, "Nenhum cliente cadastrado.", 6);
  targets.customers.replaceChildren();
  for (const customer of rows) {
    const row = document.createElement("tr");
    row.append(cell(customer.nome, "strong"), cell(customer.tipoPessoa === "PJ" ? "Pessoa jurídica" : "Pessoa física"), cell(customer.documento), cell(customer.telefone), cell(customer.email), cell(customer.endereco));
    targets.customers.append(row);
  }
}

function renderProducts(rows) {
  if (!rows.length) return empty(targets.products, "Nenhum produto cadastrado.", 7);
  targets.products.replaceChildren();
  for (const product of rows) {
    const row = document.createElement("tr");
    const name = document.createElement("td"); const strong = document.createElement("strong"); strong.className = "strong"; strong.textContent = product.nome; name.append(strong); const code = document.createElement("small"); code.textContent = product.codigo; name.append(code);
    row.append(name, cell(product.categoriaNome), cell(product.unidadeMedida));
    const priceCell = document.createElement("td"); const price = document.createElement("input"); price.type = "number"; price.min = "0"; price.step = "0.01"; price.value = product.precoUnitario.toFixed(2); price.setAttribute("aria-label", `Preço de ${product.nome}`); priceCell.append(price);
    const stockCell = document.createElement("td"); const stock = document.createElement("input"); stock.type = "number"; stock.min = "0"; stock.step = "1"; stock.value = product.estoque; stock.setAttribute("aria-label", `Estoque de ${product.nome}`); stockCell.append(stock);
    const activeCell = document.createElement("td"); const activeLabel = document.createElement("label"); activeLabel.className = "switch-label"; const active = document.createElement("input"); active.type = "checkbox"; active.checked = product.ativo; activeLabel.append(active, document.createTextNode(product.ativo ? "Ativo" : "Inativo")); activeCell.append(activeLabel);
    const actionCell = document.createElement("td"); const save = document.createElement("button"); save.type = "button"; save.className = "button small primary"; save.textContent = "Salvar";
    save.addEventListener("click", async () => {
      save.disabled = true;
      try {
        const response = await fetch(`/api/produtos/${product.id}`, { method: "PUT", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ precoUnitario: Number(price.value), estoque: Number(stock.value), ativo: active.checked }) });
        const result = await response.json(); if (!response.ok) throw new Error(result.erro || `Erro HTTP ${response.status}`);
        showMessage(`${product.nome} atualizado. O aplicativo receberá as alterações na próxima sincronização.`); await refresh();
      } catch (error) { showMessage(error.message, true); }
      finally { save.disabled = false; }
    });
    actionCell.append(save); row.append(priceCell, stockCell, activeCell, actionCell); targets.products.append(row);
  }
}

function summary() {
  $("#stat-orders").textContent = String(state.orders.length);
  $("#stat-customers").textContent = String(state.customers.length);
  $("#stat-products").textContent = String(state.products.filter(item => item.ativo).length);
  $("#stat-sales").textContent = currency.format(state.orders.reduce((sum, item) => sum + item.valorTotal, 0));
}

async function get(path) { const response = await fetch(path, { headers: { Accept: "application/json" } }); if (!response.ok) throw new Error(`Falha ao consultar ${path} (HTTP ${response.status}).`); return response.json(); }
async function refresh() {
  try {
    const [orders, customers, products] = await Promise.all([get("/api/pedidos"), get("/api/clientes"), get("/api/produtos?incluirInativos=true")]);
    state.orders = orders; state.customers = customers; state.products = products;
    renderOrders(orders); renderCustomers(customers);
    if (document.activeElement?.tagName !== "INPUT") renderProducts(products);
    summary(); setConnection(true);
    $("#last-updated").textContent = `Atualizado às ${new Intl.DateTimeFormat("pt-BR", { timeStyle: "short" }).format(new Date())}`;
  } catch (error) { setConnection(false); showMessage(`${error.message} Confira se a API e o MySQL estão ativos.`, true); }
}

document.querySelectorAll(".tab").forEach(button => button.addEventListener("click", () => {
  document.querySelectorAll(".tab").forEach(item => item.classList.toggle("active", item === button));
  document.querySelectorAll(".tab-view").forEach(view => view.classList.toggle("hidden", view.id !== `${button.dataset.tab}-view`));
}));
$("#refresh-button").addEventListener("click", refresh);
refresh(); window.setInterval(refresh, 15000);
