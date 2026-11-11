(function (root, factory) {
    if (typeof module === "object" && module.exports) module.exports = factory();
    else root.TecnovaListas = factory();
})(typeof globalThis !== "undefined" ? globalThis : this, function () {
    "use strict";
    const normal = value => String(value || "").normalize("NFD").replace(/[\u0300-\u036f]/g, "").toLocaleLowerCase();
    function deadline(task, today = new Date().toISOString().slice(0, 10)) {
        if (task.IdEstado !== 10 && task.IdEstado !== 11) return null;
        const due = String(task.FechaVencimiento || "").slice(0, 10);
        if (!/^\d{4}-\d{2}-\d{2}$/.test(due)) return null;
        const days = Math.round((Date.parse(due + "T00:00:00Z") - Date.parse(today + "T00:00:00Z")) / 86400000);
        if (days < 0) return { kind: "overdue", text: "Vencida", days };
        if (days === 0) return { kind: "today", text: "Vence hoy", days };
        if (days <= 3) return { kind: "soon", text: "Vence en " + days + (days === 1 ? " día" : " días"), days };
        return null;
    }
    function filter(rows, state, kind, today) {
        return rows.filter(row => {
            const task = kind === "tareas", project = kind === "proyectos";
            const text = task ? [row.Titulo, row.Proyecto, row.Asignado] : project ? [row.Nombre, row.Responsable] : [row.Nombre, row.Email];
            const end = String(task ? row.FechaVencimiento : row.FechaFinEstimada || "").slice(0, 10);
            const owner = task ? row.IdAsignado : row.IdResponsable;
            const warning = task ? deadline(row, today) : null;
            return (!state.search || normal(text.join(" ")).includes(normal(state.search))) &&
                (!state.state || row.IdEstado === Number(state.state)) &&
                (!state.owner || owner === Number(state.owner)) &&
                (!state.priority || row.IdPrioridad === Number(state.priority)) &&
                (!state.from || end >= state.from) && (!state.to || end <= state.to) &&
                (!state.due || (state.due === "soon" ? warning && warning.days >= 0 : warning && warning.kind === state.due));
        });
    }
    function paginate(rows, requestedPage, requestedSize) {
        const size = [5, 10, 25, 50].includes(Number(requestedSize)) ? Number(requestedSize) : 10;
        const pages = Math.max(1, Math.ceil(rows.length / size));
        const page = Math.max(1, Math.min(pages, Math.trunc(Number(requestedPage)) || 1));
        return { items: rows.slice((page - 1) * size, page * size), total: rows.length, pages, page, size,
            from: rows.length ? (page - 1) * size + 1 : 0, to: Math.min(page * size, rows.length) };
    }
    return { filter, paginate, deadline };
});
