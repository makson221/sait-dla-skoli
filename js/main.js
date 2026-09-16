const yearEl = document.getElementById("year");
if (yearEl) yearEl.textContent = new Date().getFullYear();

/* Mobile nav toggle */
const navToggle = document.getElementById("navToggle");
const mainNav = document.getElementById("mainNav");

if (navToggle && mainNav) {
  navToggle.addEventListener("click", () => {
    const isOpen = mainNav.classList.toggle("open");
    navToggle.classList.toggle("open", isOpen);
    navToggle.setAttribute("aria-expanded", String(isOpen));
  });

  mainNav.querySelectorAll("a").forEach((link) => {
    link.addEventListener("click", () => {
      mainNav.classList.remove("open");
      navToggle.classList.remove("open");
      navToggle.setAttribute("aria-expanded", "false");
    });
  });
}

/* Highlight the current page in the nav */
const currentPage = (location.pathname.split("/").pop() || "index.html");
document.querySelectorAll(".main-nav a").forEach((link) => {
  const href = link.getAttribute("href");
  if (href === currentPage || (currentPage === "" && href === "index.html")) {
    link.classList.add("active");
    link.setAttribute("aria-current", "page");
  }
});

/* Scroll reveal animations */
const revealEls = document.querySelectorAll(".reveal");
if ("IntersectionObserver" in window && revealEls.length) {
  const observer = new IntersectionObserver(
    (entries) => {
      entries.forEach((entry) => {
        if (entry.isIntersecting) {
          entry.target.classList.add("in-view");
          observer.unobserve(entry.target);
        }
      });
    },
    { threshold: 0.15 }
  );
  revealEls.forEach((el) => observer.observe(el));
} else {
  revealEls.forEach((el) => el.classList.add("in-view"));
}

/* Interactive cursor spotlight in the hero */
const hero = document.querySelector(".hero");
if (hero) {
  hero.addEventListener("pointermove", (event) => {
    const rect = hero.getBoundingClientRect();
    hero.style.setProperty("--mx", `${event.clientX - rect.left}px`);
    hero.style.setProperty("--my", `${event.clientY - rect.top}px`);
  });
}

/* Back-to-top button */
const toTop = document.getElementById("toTop");
if (toTop) {
  window.addEventListener("scroll", () => {
    toTop.classList.toggle("visible", window.scrollY > 420);
  });
}
