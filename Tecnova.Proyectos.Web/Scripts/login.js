"use strict";
const form = document.querySelector("#login-form");
const error = document.querySelector("#login-error");
document.querySelector("#mostrar-clave").addEventListener("click", event => {
    const password = document.querySelector("#contrasena");
    const show = password.type === "password";
    password.type = show ? "text" : "password";
    event.currentTarget.textContent = show ? "Ocultar" : "Mostrar";
    event.currentTarget.setAttribute("aria-pressed", String(show));
});
form.addEventListener("submit", async event => {
    event.preventDefault(); error.textContent = "";
    if (!form.email.validity.valid || !form.contrasena.value) { error.textContent = "Ingrese un correo válido y su contraseña."; return; }
    const button = form.querySelector('[type="submit"]'); button.disabled = true; button.textContent = "Verificando…";
    try {
        const response = await fetch(form.action, { method: "POST", body: new URLSearchParams(new FormData(form)), credentials: "same-origin" });
        const data = await response.json();
        if (!response.ok) throw new Error(data.mensaje || "No se pudo iniciar sesión.");
        location.replace("/");
    } catch (failure) { error.textContent = failure instanceof SyntaxError ? "No se pudo iniciar sesión. Recargue la página e intente nuevamente." : failure.message; }
    finally { button.disabled = false; button.textContent = "Ingresar →"; }
});
