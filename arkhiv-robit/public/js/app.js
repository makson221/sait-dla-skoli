import { createClient } from "https://cdn.jsdelivr.net/npm/@supabase/supabase-js@2/+esm";
import { SUPABASE_URL, SUPABASE_ANON_KEY, STORAGE_BUCKET, MAX_FILE_MB } from "./config.js";

const app = document.getElementById("app");
const nav = document.getElementById("nav");
document.getElementById("year").textContent = new Date().getFullYear();

const WORK_TYPES = { coursework: "Курсова робота", diploma: "Дипломна робота" };
const STATUSES = { pending: "На перевірці", approved: "Опубліковано", rejected: "Відхилено" };
const ROLES = { student: "Студент", teacher: "Викладач", admin: "Адміністратор" };
const ALLOWED_EXT = ["pdf", "doc", "docx", "odt", "rtf", "ppt", "pptx", "zip", "rar", "7z"];
const PAGE_SIZE = 12;

const configured = !SUPABASE_URL.includes("YOUR-PROJECT") && !SUPABASE_ANON_KEY.includes("YOUR-ANON");
const sb = configured ? createClient(SUPABASE_URL, SUPABASE_ANON_KEY) : null;

const state = { session: null, profile: null, recovering: false };
const isStaff = () => ["teacher", "admin"].includes(state.profile?.role);
const isAdmin = () => state.profile?.role === "admin";

/* ------------------------------------------------------------------ */
/*  Helpers                                                            */
/* ------------------------------------------------------------------ */

// Creates DOM elements without innerHTML, so user text can never inject markup.
function h(tag, attrs = {}, ...children) {
  const node = document.createElement(tag);
  for (const [key, value] of Object.entries(attrs || {})) {
    if (value === null || value === undefined || value === false) continue;
    if (key.startsWith("on")) node.addEventListener(key.slice(2), value);
    else if (key === "class") node.className = value;
    else if (value === true) node.setAttribute(key, "");
    else node.setAttribute(key, value);
  }
  for (const child of children.flat()) {
    if (child === null || child === undefined || child === false) continue;
    node.append(child instanceof Node ? child : String(child));
  }
  return node;
}

function render(...nodes) {
  app.replaceChildren(...nodes);
  window.scrollTo(0, 0);
}

let toastTimer;
function toast(message, type = "ok") {
  const el = document.getElementById("toast");
  el.textContent = message;
  el.className = `toast show ${type}`;
  clearTimeout(toastTimer);
  toastTimer = setTimeout(() => (el.className = "toast"), 4000);
}

function formatSize(bytes) {
  if (!bytes && bytes !== 0) return "";
  if (bytes < 1024 * 1024) return `${Math.max(1, Math.round(bytes / 1024))} КБ`;
  return `${(bytes / 1024 / 1024).toFixed(1)} МБ`;
}

function formatDate(value) {
  return value ? new Date(value).toLocaleDateString("uk-UA") : "";
}

function statusBadge(status) {
  return h("span", { class: `badge status-${status}` }, STATUSES[status] || status);
}

function typeBadge(type) {
  return h("span", { class: `badge type-${type}` }, WORK_TYPES[type] || type);
}

// Removes characters that have special meaning in PostgREST filters.
function cleanSearch(text) {
  return text.replace(/[,()%*\\]/g, " ").trim();
}

function field(label, input, hint) {
  return h("label", { class: "field" }, h("span", { class: "field-label" }, label), input, hint && h("small", { class: "muted" }, hint));
}

function loading() {
  render(h("p", { class: "muted" }, "Завантаження…"));
}

function errorView(message) {
  render(h("div", { class: "card empty" }, h("h2", {}, "Щось пішло не так"), h("p", {}, message), h("a", { class: "btn", href: "#/" }, "До каталогу")));
}

function requireLogin() {
  if (state.session) return true;
  sessionStorage.setItem("afterLogin", location.hash);
  location.hash = "#/login";
  return false;
}

function translateError(error) {
  const msg = error?.message || String(error);
  if (/Invalid login credentials/i.test(msg)) return "Невірний email або пароль.";
  if (/Email not confirmed/i.test(msg)) return "Email ще не підтверджено. Перевірте пошту.";
  if (/User already registered/i.test(msg)) return "Користувач з таким email вже існує.";
  if (/Password should be at least/i.test(msg)) return "Пароль має містити щонайменше 6 символів.";
  if (/exceeded the maximum allowed size|Payload too large/i.test(msg)) return `Файл завеликий (максимум ${MAX_FILE_MB} МБ).`;
  if (/row-level security/i.test(msg)) return "Недостатньо прав для цієї дії.";
  return msg;
}

/* ------------------------------------------------------------------ */
/*  Navigation                                                         */
/* ------------------------------------------------------------------ */

function renderNav() {
  const route = location.hash.split("?")[0] || "#/";
  const link = (href, text) => h("a", { href, class: route === href ? "active" : null }, text);
  const items = [link("#/", "Каталог")];
  if (state.session) {
    items.push(link("#/upload", "Додати роботу"), link("#/my", "Мої роботи"));
    if (isStaff()) items.push(link("#/admin", "Модерація"));
    items.push(
      h("span", { class: "nav-user" }, state.profile?.full_name || state.session.user.email,
        state.profile && h("small", {}, ROLES[state.profile.role])),
      h("button", { class: "btn btn-ghost", onclick: logout }, "Вийти"),
    );
  } else {
    items.push(link("#/login", "Увійти"), h("a", { href: "#/register", class: "btn" }, "Реєстрація"));
  }
  nav.replaceChildren(...items);
}

const navToggle = document.getElementById("navToggle");
navToggle.addEventListener("click", () => {
  const open = nav.classList.toggle("open");
  navToggle.setAttribute("aria-expanded", String(open));
});
nav.addEventListener("click", (e) => {
  if (e.target.closest("a,button")) {
    nav.classList.remove("open");
    navToggle.setAttribute("aria-expanded", "false");
  }
});

/* ------------------------------------------------------------------ */
/*  Router                                                             */
/* ------------------------------------------------------------------ */

async function router() {
  renderNav();
  if (!configured) return setupView();
  if (state.recovering) return newPasswordView();

  const [path, query] = (location.hash.slice(1) || "/").split("?");
  const params = new URLSearchParams(query || "");
  const parts = path.split("/").filter(Boolean);

  try {
    switch (parts[0]) {
      case undefined: return await catalogView(params);
      case "project": return await projectView(parts[1]);
      case "upload": return requireLogin() && (await projectFormView());
      case "edit": return requireLogin() && (await projectFormView(parts[1]));
      case "my": return requireLogin() && (await myProjectsView());
      case "admin": return requireLogin() && (await adminView(params));
      case "login": return loginView();
      case "register": return registerView();
      case "forgot": return forgotView();
      default: return errorView("Сторінку не знайдено.");
    }
  } catch (err) {
    console.error(err);
    errorView(translateError(err));
  }
}

/* ------------------------------------------------------------------ */
/*  Views                                                              */
/* ------------------------------------------------------------------ */

function setupView() {
  render(h("div", { class: "card" },
    h("h1", {}, "Потрібне налаштування"),
    h("p", {}, "Застосунок ще не підключено до бази даних. Відкрийте файл ", h("code", {}, "js/config.js"),
      " і вкажіть ", h("code", {}, "SUPABASE_URL"), " та ", h("code", {}, "SUPABASE_ANON_KEY"),
      " свого проєкту Supabase (Project Settings → API)."),
    h("p", {}, "Покрокова інструкція є у файлі README.md."),
  ));
}

async function catalogView(params) {
  const q = params.get("q") || "";
  const type = params.get("type") || "";
  const year = params.get("year") || "";
  const page = Math.max(1, Number(params.get("page")) || 1);

  const form = h("form", { class: "filters card", onsubmit: (e) => {
    e.preventDefault();
    const data = new FormData(e.target);
    const next = new URLSearchParams();
    for (const [k, v] of data) if (String(v).trim()) next.set(k, String(v).trim());
    location.hash = `#/?${next}`;
  } },
    h("input", { name: "q", type: "search", value: q, placeholder: "Пошук: тема, автор, керівник, ключові слова…", "aria-label": "Пошук" }),
    h("select", { name: "type", "aria-label": "Тип роботи" },
      h("option", { value: "" }, "Усі типи"),
      ...Object.entries(WORK_TYPES).map(([v, t]) => h("option", { value: v, selected: v === type }, t))),
    h("input", { name: "year", type: "number", min: 1990, max: 2100, value: year, placeholder: "Рік", "aria-label": "Рік захисту" }),
    h("button", { class: "btn", type: "submit" }, "Знайти"),
    (q || type || year) && h("a", { class: "btn btn-ghost", href: "#/" }, "Скинути"),
  );

  const results = h("div", {}, h("p", { class: "muted" }, "Завантаження…"));
  render(
    h("section", { class: "hero" },
      h("h1", {}, "Архів курсових і дипломних робіт"),
      h("p", {}, "Шукайте, переглядайте та завантажуйте роботи студентів. Щоб додати свою роботу — увійдіть або зареєструйтеся."),
    ),
    form,
    results,
  );

  let query = sb.from("projects")
    .select("id,title,work_type,author_name,group_name,specialty,supervisor,year,created_at", { count: "exact" })
    .eq("status", "approved")
    .order("created_at", { ascending: false })
    .range((page - 1) * PAGE_SIZE, page * PAGE_SIZE - 1);
  if (type) query = query.eq("work_type", type);
  if (year) query = query.eq("year", Number(year));
  const term = cleanSearch(q);
  if (term) {
    const like = `%${term}%`;
    query = query.or(["title", "author_name", "supervisor", "specialty", "keywords", "group_name"].map((c) => `${c}.ilike.${like}`).join(","));
  }

  const { data, error, count } = await query;
  if (error) throw error;

  if (!data.length) {
    results.replaceChildren(h("div", { class: "card empty" }, h("p", {}, q || type || year ? "За вашим запитом нічого не знайдено." : "Поки що тут немає опублікованих робіт.")));
    return;
  }

  const pages = Math.ceil(count / PAGE_SIZE);
  const pageLink = (p) => {
    const next = new URLSearchParams(params);
    next.set("page", p);
    return `#/?${next}`;
  };
  results.replaceChildren(
    h("p", { class: "muted" }, `Знайдено робіт: ${count}`),
    h("div", { class: "grid" }, data.map(projectCard)),
    pages > 1 && h("div", { class: "pager" },
      page > 1 && h("a", { class: "btn btn-ghost", href: pageLink(page - 1) }, "← Назад"),
      h("span", {}, `Сторінка ${page} з ${pages}`),
      page < pages && h("a", { class: "btn btn-ghost", href: pageLink(page + 1) }, "Далі →")),
  );
}

function projectCard(p) {
  return h("a", { class: "card project-card", href: `#/project/${p.id}` },
    h("div", { class: "card-top" }, typeBadge(p.work_type), h("span", { class: "muted" }, p.year), p.status && p.status !== "approved" && statusBadge(p.status)),
    h("h3", {}, p.title),
    h("p", { class: "muted" }, [p.author_name, p.group_name].filter(Boolean).join(", ")),
    p.supervisor && h("p", { class: "small" }, "Керівник: ", p.supervisor),
    p.specialty && h("p", { class: "small muted" }, p.specialty),
  );
}

async function projectView(id) {
  loading();
  const { data: p, error } = await sb.from("projects").select("*").eq("id", id).maybeSingle();
  if (error) throw error;
  if (!p) return errorView("Роботу не знайдено або вона ще не опублікована.");

  const isOwner = state.session?.user.id === p.owner_id;
  const rows = [
    ["Автор", p.author_name],
    ["Група", p.group_name],
    ["Спеціальність", p.specialty],
    ["Науковий керівник", p.supervisor],
    ["Рік захисту", p.year],
    ["Ключові слова", p.keywords],
    ["Додано", formatDate(p.created_at)],
  ].filter(([, v]) => v);

  const actions = h("div", { class: "actions" });
  if (state.session) {
    actions.append(h("button", { class: "btn", onclick: () => download(p) }, "⬇ Завантажити файл"));
  } else {
    actions.append(h("a", { class: "btn", href: "#/login", onclick: () => sessionStorage.setItem("afterLogin", location.hash) }, "Увійдіть, щоб завантажити файл"));
  }
  if (isOwner || isStaff()) actions.append(h("a", { class: "btn btn-ghost", href: `#/edit/${p.id}` }, "Редагувати"));
  if (isOwner || isAdmin()) actions.append(h("button", { class: "btn btn-danger", onclick: () => deleteProject(p) }, "Видалити"));

  render(
    h("a", { class: "back", href: "#/" }, "← До каталогу"),
    h("article", { class: "card project-detail" },
      h("div", { class: "card-top" }, typeBadge(p.work_type), statusBadge(p.status)),
      h("h1", {}, p.title),
      p.status === "rejected" && p.review_comment && h("div", { class: "notice notice-error" }, h("strong", {}, "Причина відхилення: "), p.review_comment),
      p.status === "approved" && p.review_comment && (isOwner || isStaff()) && h("div", { class: "notice" }, h("strong", {}, "Коментар викладача: "), p.review_comment),
      p.status === "pending" && h("div", { class: "notice" }, "Робота очікує перевірки викладачем і поки що не видна в каталозі."),
      h("dl", { class: "meta" }, rows.flatMap(([k, v]) => [h("dt", {}, k), h("dd", {}, v)])),
      p.description && h("section", {}, h("h2", {}, "Анотація"), h("p", { class: "description" }, p.description)),
      h("div", { class: "file-box" }, "📄 ", h("strong", {}, p.file_name), h("span", { class: "muted" }, ` · ${formatSize(p.file_size)}`)),
      actions,
      isStaff() && reviewPanel(p),
    ),
  );
}

function reviewPanel(p) {
  const comment = h("textarea", { rows: 3, placeholder: "Коментар для автора (обов’язковий при відхиленні)" }, p.review_comment || "");
  const setStatus = async (status) => {
    if (status === "rejected" && !comment.value.trim()) return toast("Вкажіть причину відхилення.", "error");
    const { error } = await sb.from("projects").update({ status, review_comment: comment.value.trim() || null }).eq("id", p.id);
    if (error) return toast(translateError(error), "error");
    toast(status === "approved" ? "Роботу опубліковано." : status === "rejected" ? "Роботу відхилено." : "Статус змінено.");
    router();
  };
  return h("section", { class: "review" },
    h("h2", {}, "Перевірка роботи"),
    comment,
    h("div", { class: "actions" },
      h("button", { class: "btn btn-success", onclick: () => setStatus("approved") }, "✓ Опублікувати"),
      h("button", { class: "btn btn-danger", onclick: () => setStatus("rejected") }, "✕ Відхилити"),
      p.status !== "pending" && h("button", { class: "btn btn-ghost", onclick: () => setStatus("pending") }, "Повернути на перевірку")),
  );
}

async function download(p) {
  const { data, error } = await sb.storage.from(STORAGE_BUCKET).createSignedUrl(p.file_path, 120, { download: p.file_name });
  if (error) return toast(translateError(error), "error");
  window.location.href = data.signedUrl;
}

async function deleteProject(p) {
  if (!confirm(`Видалити роботу «${p.title}»? Цю дію не можна скасувати.`)) return;
  const { error } = await sb.from("projects").delete().eq("id", p.id);
  if (error) return toast(translateError(error), "error");
  await sb.storage.from(STORAGE_BUCKET).remove([p.file_path]);
  toast("Роботу видалено.");
  location.hash = "#/my";
}

async function projectFormView(id) {
  loading();
  let p = null;
  if (id) {
    const res = await sb.from("projects").select("*").eq("id", id).maybeSingle();
    if (res.error) throw res.error;
    p = res.data;
    if (!p) return errorView("Роботу не знайдено.");
  }

  const val = (k, fallback = "") => (p ? p[k] ?? "" : fallback);
  const fileInput = h("input", { type: "file", name: "file", accept: ALLOWED_EXT.map((e) => "." + e).join(","), required: !p });
  const submit = h("button", { class: "btn", type: "submit" }, p ? "Зберегти зміни" : "Надіслати на перевірку");

  const form = h("form", { class: "card form", onsubmit: async (e) => {
    e.preventDefault();
    const data = Object.fromEntries(new FormData(e.target));
    const file = fileInput.files[0];

    if (file) {
      const ext = file.name.split(".").pop().toLowerCase();
      if (!ALLOWED_EXT.includes(ext)) return toast(`Непідтримуваний формат. Дозволено: ${ALLOWED_EXT.join(", ")}`, "error");
      if (file.size > MAX_FILE_MB * 1024 * 1024) return toast(`Файл завеликий (максимум ${MAX_FILE_MB} МБ).`, "error");
    }

    submit.disabled = true;
    submit.textContent = file ? "Завантаження файлу…" : "Збереження…";

    const record = {
      work_type: data.work_type,
      title: data.title.trim(),
      author_name: data.author_name.trim(),
      group_name: data.group_name.trim() || null,
      specialty: data.specialty.trim() || null,
      supervisor: data.supervisor.trim() || null,
      year: Number(data.year),
      keywords: data.keywords.trim() || null,
      description: data.description.trim() || null,
    };

    let newPath = null;
    try {
      if (file) {
        // The storage key uses only ASCII characters; the original name is kept in the database.
        const ext = file.name.split(".").pop().toLowerCase();
        newPath = `${state.session.user.id}/${crypto.randomUUID()}.${ext}`;
        const up = await sb.storage.from(STORAGE_BUCKET).upload(newPath, file, { contentType: file.type || "application/octet-stream" });
        if (up.error) throw up.error;
        Object.assign(record, { file_path: newPath, file_name: file.name, file_size: file.size, mime_type: file.type || null });
      }

      const res = p
        ? await sb.from("projects").update(record).eq("id", p.id).select("id").single()
        : await sb.from("projects").insert(record).select("id").single();
      if (res.error) throw res.error;

      if (p && newPath && p.file_path !== newPath) await sb.storage.from(STORAGE_BUCKET).remove([p.file_path]);
      toast(p ? "Зміни збережено." : isStaff() ? "Роботу додано." : "Роботу надіслано на перевірку.");
      location.hash = `#/project/${res.data.id}`;
    } catch (err) {
      if (newPath) await sb.storage.from(STORAGE_BUCKET).remove([newPath]);
      toast(translateError(err), "error");
      submit.disabled = false;
      submit.textContent = p ? "Зберегти зміни" : "Надіслати на перевірку";
    }
  } },
    h("h1", {}, p ? "Редагування роботи" : "Додати роботу"),
    !p && !isStaff() && h("p", { class: "muted" }, "Після надсилання робота з’явиться в каталозі, коли її перевірить викладач."),
    p && !isStaff() && h("p", { class: "notice" }, "Після збереження змін робота знову піде на перевірку."),
    h("div", { class: "form-grid" },
      field("Тип роботи *", h("select", { name: "work_type", required: true },
        ...Object.entries(WORK_TYPES).map(([v, t]) => h("option", { value: v, selected: val("work_type") === v }, t)))),
      field("Рік захисту *", h("input", { name: "year", type: "number", min: 1990, max: 2100, required: true, value: val("year", new Date().getFullYear()) })),
    ),
    field("Тема роботи *", h("input", { name: "title", required: true, minlength: 3, maxlength: 300, value: val("title") })),
    h("div", { class: "form-grid" },
      field("ПІБ автора *", h("input", { name: "author_name", required: true, minlength: 2, maxlength: 200, value: val("author_name", state.profile?.full_name || "") })),
      field("Група", h("input", { name: "group_name", value: val("group_name", state.profile?.group_name || "") })),
    ),
    h("div", { class: "form-grid" },
      field("Спеціальність", h("input", { name: "specialty", value: val("specialty"), placeholder: "Напр. 121 Інженерія програмного забезпечення" })),
      field("Науковий керівник", h("input", { name: "supervisor", value: val("supervisor") })),
    ),
    field("Ключові слова", h("input", { name: "keywords", value: val("keywords"), placeholder: "Через кому" })),
    field("Анотація", h("textarea", { name: "description", rows: 5 }, val("description"))),
    field(p ? "Замінити файл (необов’язково)" : "Файл роботи *", fileInput,
      `${ALLOWED_EXT.map((e) => e.toUpperCase()).join(", ")} · до ${MAX_FILE_MB} МБ` + (p ? ` · поточний: ${p.file_name}` : "")),
    h("div", { class: "actions" }, submit, h("a", { class: "btn btn-ghost", href: p ? `#/project/${p.id}` : "#/" }, "Скасувати")),
  );
  render(form);
}

async function myProjectsView() {
  loading();
  const { data, error } = await sb.from("projects")
    .select("id,title,work_type,author_name,group_name,supervisor,specialty,year,status,review_comment,created_at")
    .eq("owner_id", state.session.user.id)
    .order("created_at", { ascending: false });
  if (error) throw error;

  render(
    h("div", { class: "page-head" }, h("h1", {}, "Мої роботи"), h("a", { class: "btn", href: "#/upload" }, "+ Додати роботу")),
    data.length
      ? h("div", { class: "grid" }, data.map((p) => {
        const card = projectCard(p);
        if (p.status === "rejected" && p.review_comment) card.append(h("p", { class: "small error-text" }, "Причина: ", p.review_comment));
        return card;
      }))
      : h("div", { class: "card empty" }, h("p", {}, "Ви ще не додали жодної роботи."), h("a", { class: "btn", href: "#/upload" }, "Додати першу роботу")),
  );
}

async function adminView(params) {
  if (!isStaff()) return errorView("Цей розділ доступний лише викладачам та адміністраторам.");
  loading();
  const tab = params.get("tab") || "pending";
  const tabs = [["pending", "На перевірці"], ["all", "Усі роботи"], ["stats", "Статистика"]];
  if (isAdmin()) tabs.push(["users", "Користувачі"]);

  const content = h("div", {}, h("p", { class: "muted" }, "Завантаження…"));
  render(
    h("h1", {}, "Модерація"),
    h("div", { class: "tabs" }, tabs.map(([k, t]) => h("a", { href: `#/admin?tab=${k}`, class: k === tab ? "active" : null }, t))),
    content,
  );

  if (tab === "users" && isAdmin()) return content.replaceChildren(await usersTable());
  if (tab === "stats") return content.replaceChildren(await statsView());

  let query = sb.from("projects")
    .select("id,title,work_type,author_name,group_name,year,status,created_at")
    .order("created_at", { ascending: tab === "pending" });
  if (tab === "pending") query = query.eq("status", "pending");
  const { data, error } = await query;
  if (error) throw error;

  if (!data.length) return content.replaceChildren(h("div", { class: "card empty" }, h("p", {}, tab === "pending" ? "Немає робіт, що очікують перевірки. 🎉" : "Робіт ще немає.")));

  const quick = async (p, status) => {
    let review_comment = null;
    if (status === "rejected") {
      review_comment = prompt("Причина відхилення:");
      if (!review_comment?.trim()) return;
    }
    const { error: err } = await sb.from("projects").update({ status, review_comment }).eq("id", p.id);
    if (err) return toast(translateError(err), "error");
    toast(status === "approved" ? "Опубліковано." : "Відхилено.");
    router();
  };

  content.replaceChildren(h("div", { class: "table-wrap card" }, h("table", {},
    h("thead", {}, h("tr", {}, ["Тема", "Тип", "Автор", "Рік", "Додано", "Статус", ""].map((t) => h("th", {}, t)))),
    h("tbody", {}, data.map((p) => h("tr", {},
      h("td", {}, h("a", { href: `#/project/${p.id}` }, p.title)),
      h("td", {}, WORK_TYPES[p.work_type]),
      h("td", {}, [p.author_name, p.group_name].filter(Boolean).join(", ")),
      h("td", {}, p.year),
      h("td", {}, formatDate(p.created_at)),
      h("td", {}, statusBadge(p.status)),
      h("td", { class: "row-actions" },
        p.status !== "approved" && h("button", { class: "btn btn-small btn-success", title: "Опублікувати", onclick: () => quick(p, "approved") }, "✓"),
        p.status !== "rejected" && h("button", { class: "btn btn-small btn-danger", title: "Відхилити", onclick: () => quick(p, "rejected") }, "✕")),
    ))),
  )));
}

async function statsView() {
  const { data, error } = await sb.from("projects").select("work_type,status,year");
  if (error) throw error;
  const count = (fn) => data.filter(fn).length;
  const tiles = [
    ["Усього робіт", data.length],
    ["Опубліковано", count((p) => p.status === "approved")],
    ["На перевірці", count((p) => p.status === "pending")],
    ["Відхилено", count((p) => p.status === "rejected")],
    ["Курсових", count((p) => p.work_type === "coursework")],
    ["Дипломних", count((p) => p.work_type === "diploma")],
  ];
  const byYear = {};
  for (const p of data) byYear[p.year] = (byYear[p.year] || 0) + 1;
  return h("div", {},
    h("div", { class: "stats" }, tiles.map(([t, n]) => h("div", { class: "card stat" }, h("strong", {}, n), h("span", {}, t)))),
    Object.keys(byYear).length > 0 && h("div", { class: "card table-wrap" }, h("table", {},
      h("thead", {}, h("tr", {}, h("th", {}, "Рік"), h("th", {}, "Кількість робіт"))),
      h("tbody", {}, Object.entries(byYear).sort((a, b) => b[0] - a[0]).map(([y, n]) => h("tr", {}, h("td", {}, y), h("td", {}, n)))))),
  );
}

async function usersTable() {
  const { data, error } = await sb.from("profiles").select("id,email,full_name,group_name,role,created_at").order("created_at");
  if (error) throw error;
  return h("div", { class: "table-wrap card" }, h("table", {},
    h("thead", {}, h("tr", {}, ["ПІБ", "Email", "Група", "Зареєстровано", "Роль"].map((t) => h("th", {}, t)))),
    h("tbody", {}, data.map((u) => h("tr", {},
      h("td", {}, u.full_name || "—"),
      h("td", {}, u.email),
      h("td", {}, u.group_name || "—"),
      h("td", {}, formatDate(u.created_at)),
      h("td", {}, h("select", {
        disabled: u.id === state.session.user.id,
        onchange: async (e) => {
          const { error: err } = await sb.from("profiles").update({ role: e.target.value }).eq("id", u.id);
          if (err) { toast(translateError(err), "error"); e.target.value = u.role; return; }
          u.role = e.target.value;
          toast("Роль змінено.");
        },
      }, Object.entries(ROLES).map(([v, t]) => h("option", { value: v, selected: v === u.role }, t)))),
    ))),
  ));
}

/* ------------------------------------------------------------------ */
/*  Auth views                                                         */
/* ------------------------------------------------------------------ */

function authCard(title, form, footer) {
  render(h("div", { class: "auth" }, h("div", { class: "card" }, h("h1", {}, title), form, footer && h("p", { class: "auth-footer" }, footer))));
}

function afterLogin() {
  const target = sessionStorage.getItem("afterLogin");
  sessionStorage.removeItem("afterLogin");
  location.hash = target && !/login|register/.test(target) ? target : "#/";
}

function loginView() {
  if (state.session) return afterLogin();
  const btn = h("button", { class: "btn btn-block", type: "submit" }, "Увійти");
  authCard("Вхід", h("form", { class: "form", onsubmit: async (e) => {
    e.preventDefault();
    const d = Object.fromEntries(new FormData(e.target));
    btn.disabled = true;
    const { error } = await sb.auth.signInWithPassword({ email: d.email.trim(), password: d.password });
    btn.disabled = false;
    if (error) return toast(translateError(error), "error");
    toast("Ви увійшли.");
  } },
    field("Email", h("input", { name: "email", type: "email", required: true, autocomplete: "email" })),
    field("Пароль", h("input", { name: "password", type: "password", required: true, autocomplete: "current-password" })),
    btn,
    h("a", { href: "#/forgot", class: "small" }, "Забули пароль?"),
  ), ["Немає акаунта? ", h("a", { href: "#/register" }, "Зареєструватися")]);
}

function registerView() {
  if (state.session) return afterLogin();
  const btn = h("button", { class: "btn btn-block", type: "submit" }, "Зареєструватися");
  authCard("Реєстрація", h("form", { class: "form", onsubmit: async (e) => {
    e.preventDefault();
    const d = Object.fromEntries(new FormData(e.target));
    if (d.password !== d.password2) return toast("Паролі не збігаються.", "error");
    btn.disabled = true;
    const { data, error } = await sb.auth.signUp({
      email: d.email.trim(),
      password: d.password,
      options: {
        data: { full_name: d.full_name.trim(), group_name: d.group_name.trim() },
        emailRedirectTo: location.origin + location.pathname,
      },
    });
    btn.disabled = false;
    if (error) return toast(translateError(error), "error");
    if (!data.session) {
      render(h("div", { class: "auth" }, h("div", { class: "card" },
        h("h1", {}, "Перевірте пошту"),
        h("p", {}, "Ми надіслали лист для підтвердження на ", h("strong", {}, d.email), ". Перейдіть за посиланням у листі, щоб завершити реєстрацію."))));
    }
  } },
    field("ПІБ *", h("input", { name: "full_name", required: true, minlength: 2, autocomplete: "name" })),
    field("Група", h("input", { name: "group_name", placeholder: "Напр. КН-41" })),
    field("Email *", h("input", { name: "email", type: "email", required: true, autocomplete: "email" })),
    field("Пароль *", h("input", { name: "password", type: "password", required: true, minlength: 6, autocomplete: "new-password" })),
    field("Повторіть пароль *", h("input", { name: "password2", type: "password", required: true, minlength: 6, autocomplete: "new-password" })),
    btn,
  ), ["Вже маєте акаунт? ", h("a", { href: "#/login" }, "Увійти")]);
}

function forgotView() {
  authCard("Відновлення пароля", h("form", { class: "form", onsubmit: async (e) => {
    e.preventDefault();
    const email = new FormData(e.target).get("email").trim();
    const { error } = await sb.auth.resetPasswordForEmail(email, { redirectTo: location.origin + location.pathname });
    if (error) return toast(translateError(error), "error");
    toast("Лист із посиланням для зміни пароля надіслано.");
    location.hash = "#/login";
  } },
    field("Email", h("input", { name: "email", type: "email", required: true })),
    h("button", { class: "btn btn-block", type: "submit" }, "Надіслати посилання"),
  ), [h("a", { href: "#/login" }, "← Назад до входу")]);
}

function newPasswordView() {
  authCard("Новий пароль", h("form", { class: "form", onsubmit: async (e) => {
    e.preventDefault();
    const password = new FormData(e.target).get("password");
    const { error } = await sb.auth.updateUser({ password });
    if (error) return toast(translateError(error), "error");
    state.recovering = false;
    toast("Пароль змінено.");
    location.hash = "#/";
    router();
  } },
    field("Новий пароль", h("input", { name: "password", type: "password", required: true, minlength: 6, autocomplete: "new-password" })),
    h("button", { class: "btn btn-block", type: "submit" }, "Зберегти"),
  ));
}

async function logout() {
  await sb.auth.signOut();
  toast("Ви вийшли з акаунта.");
  location.hash = "#/";
}

/* ------------------------------------------------------------------ */
/*  Start                                                              */
/* ------------------------------------------------------------------ */

async function loadProfile() {
  if (!state.session) return (state.profile = null);
  const { data } = await sb.from("profiles").select("*").eq("id", state.session.user.id).maybeSingle();
  state.profile = data;
}

async function start() {
  if (!configured) return router();

  const { data } = await sb.auth.getSession();
  state.session = data.session;
  await loadProfile();

  sb.auth.onAuthStateChange((event, session) => {
    const wasLoggedIn = Boolean(state.session);
    state.session = session;
    if (event === "PASSWORD_RECOVERY") state.recovering = true;
    // Supabase recommends not awaiting other Supabase calls inside this callback.
    setTimeout(async () => {
      await loadProfile();
      if (event === "SIGNED_IN" && !wasLoggedIn && /^#\/(login|register)?$/.test(location.hash || "#/")) afterLogin();
      router();
    }, 0);
  });

  window.addEventListener("hashchange", router);
  router();
}

start();
