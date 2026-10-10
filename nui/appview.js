const extView = document.getElementById("ext-view");

function appAction(app, action, data) {
  if (action) post("appAction", { app, action, data: data ?? {} });
}

function appItem(app, item) {
  const image = item.image ? iconImage(item.image, "av-image") : null;
  const button = el("button", { className: `av-item${item.active ? " active" : ""}` }, [
    image,
    el("span", { className: "av-body" }, [
      el("span", { className: "av-title", text: item.title }),
      item.detail ? el("span", { className: "av-detail", text: item.detail }) : null,
    ]),
  ]);
  button.type = "button";
  button.disabled = Boolean(item.disabled) || !item.action;
  button.addEventListener("click", () => appAction(app, item.action, item.data));
  return button;
}

function appButton(app, item) {
  const classes = ["button"];
  if (item.active) classes.push("active");
  if (item.style === "primary" || item.style === "danger") classes.push(item.style);

  const button = el("button", { className: classes.join(" "), text: item.title });
  button.type = "button";
  button.disabled = Boolean(item.disabled) || !item.action;
  button.addEventListener("click", () => appAction(app, item.action, item.data));
  return button;
}

function appForm(app, block) {
  const form = el("form", { className: "av-form" }, block.fields.map((field) => {
    const input = el("input");
    input.name = field.name;
    input.type = field.type || "text";
    input.placeholder = field.placeholder ?? "";
    input.value = field.value ?? "";
    input.autocomplete = "off";
    input.spellcheck = false;
    return el("label", { className: "av-field" }, [el("span", { text: field.label }), input]);
  }));

  const submit = el("button", { className: "button primary", text: block.submit || "Save" });
  submit.type = "submit";
  form.append(submit);

  form.addEventListener("submit", (event) => {
    event.preventDefault();
    appAction(app, block.action, Object.fromEntries(new FormData(form).entries()));
  });
  return form;
}

function appBlock(app, block) {
  switch (block.type) {
    case "section":
      return el("p", { className: "panel-title av-section", text: block.title });
    case "text":
      return el("p", { className: `av-text${block.muted ? " muted" : ""}`, text: block.text });
    case "buttons":
      return el("div", { className: "av-buttons" }, block.items.map((item) => appButton(app, item)));
    case "list": {
      const grid = block.layout === "grid";
      return el("div", { className: `av-list${grid ? " grid" : ""}` }, block.items.map((item) => appItem(app, item)));
    }
    case "form":
      return appForm(app, block);
    default:
      return null;
  }
}

function renderAppView(view) {
  const focused = extView.contains(document.activeElement) && document.activeElement.tagName === "INPUT";
  if (focused) return;

  extView.replaceChildren(...(view?.screen?.blocks ?? []).map((block) => appBlock(view.app, block)).filter(Boolean));
}
