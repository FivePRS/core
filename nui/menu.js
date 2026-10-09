const menuRoot = document.getElementById("menu");
const menuItems = document.getElementById("menu-items");
const menuSubtitle = document.getElementById("menu-subtitle");

function menuItem(item, selected) {
  const classes = ["cmenu-item"];
  if (selected) classes.push("selected");
  if (item.disabled) classes.push("disabled");

  return el("li", { className: classes.join(" ") }, [
    el("span", { className: "cmenu-label", text: item.label }),
    item.detail ? el("span", { className: "cmenu-detail", text: item.detail }) : null,
    item.hasSubmenu ? el("span", { className: "cmenu-arrow", text: "›" }) : null,
  ]);
}

screens.menu = {
  show(view) {
    menuRoot.hidden = false;
    const iconSlot = document.getElementById("menu-icon");
    if (iconSlot.dataset.src !== (view.icon ?? "")) {
      iconSlot.dataset.src = view.icon ?? "";
      const icon = iconImage(view.icon);
      iconSlot.replaceChildren(...(icon ? [icon] : []));
    }
    setText("menu-title", view.title);
    menuSubtitle.textContent = view.subtitle ?? "";
    menuSubtitle.hidden = !view.subtitle;
    setText("menu-back", view.back ? "Back" : "Close");

    menuItems.replaceChildren(...view.items.map((item, index) => menuItem(item, index === view.index)));
    menuItems.children[view.index]?.scrollIntoView({ block: "nearest" });
  },
  hide() {
    menuRoot.hidden = true;
  },
};
