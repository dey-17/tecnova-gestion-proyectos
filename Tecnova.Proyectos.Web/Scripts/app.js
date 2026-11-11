"use strict";
(() => {
    const $ = selector => document.querySelector(selector);
    const content = $("#contenido"), dialog = $("#editor"), form = $("#editor-form");
    const admin = document.body.dataset.role === "1", userId = Number(document.body.dataset.userId);
    const csrf = $("#csrf input").value;
    const escape = value => String(value ?? "").replace(/[&<>"']/g, c => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]));
    const date = value => value ? String(value).slice(0, 10).split("-").reverse().join("/") : "—";
    const iso = value => value ? String(value).slice(0, 10) : "";
    const stateClass = id => ({ 1: "active", 2: "closed", 10: "pending", 11: "working", 12: "done", 13: "closed" }[id] || "");
    const pill = (name, id) => `<span class="status ${stateClass(id)}">${escape(name)}</span>`;
    const icons = {
        view: '<path d="M2 12s3.5-7 10-7 10 7 10 7-3.5 7-10 7S2 12 2 12Z"/><circle cx="12" cy="12" r="3"/>',
        edit: '<path d="m14 5 5 5M4 20l5-1L21 7a2 2 0 0 0-5-5L4 14v6Z"/>',
        delete: '<path d="M3 6h18M9 6V3h6v3M5 6l1 15h12l1-15M10 10v7M14 10v7"/>',
        history: '<path d="M3 11a9 9 0 1 1 2 7M3 4v7h7M12 7v5l3 2"/>',
        lock: '<rect x="5" y="10" width="14" height="11" rx="2"/><path d="M8 10V7a4 4 0 0 1 8 0v3M12 14v3"/>'
    };
    const icon = kind => `<svg class="action-icon" viewBox="0 0 24 24" aria-hidden="true" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round">${icons[kind] || ''}</svg>`;
    const action = (name, label, id = "", cls = "secondary") => `<button type="button" class="${cls}" data-action="${name}" data-id="${id}">${icons[name.split('-')[0]] ? icon(name.split('-')[0]) : ''}${label}</button>`;
    const restricted = (label, reason) => `<button type="button" class="text-button restricted" data-action="explain" data-reason="${escape(reason)}" aria-label="${escape(label)}: ver motivo de restricción">${icon('lock')}${label}</button>`;
    const lists = window.TecnovaListas, listStates = new Map();
    let editorResource = '', editorNew = false, lastSaved = null, savedOutsideFilter = false;
    let renderList = null;
    function dueLabel(task) {
        const warning = lists.deadline(task);
        return warning ? `<small class="deadline ${warning.kind}">${escape(warning.text)}</small>` : '';
    }
    function taskActions(task) {
        const closed = task.EstadoProyecto !== 1;
        const owner = admin || task.IdAsignado === userId;
        const reason = closed ? 'Este proyecto está cerrado. Sus tareas se conservan solo para consulta.' : !owner ? 'No tiene permisos para esta acción. Solo la persona asignada o un administrador puede modificar esta tarea.' : '';
        const deleteReason = reason || (task.IdEstado !== 10 ? 'Solo se pueden eliminar tareas pendientes. Para conservar el historial de una tarea iniciada, cambie su estado a Cancelada.' : '');
        return action('view-task', 'Ver', task.Id, 'text-button') + (reason ? restricted('Editar', reason) : action('edit-task', 'Editar', task.Id, 'text-button')) + (deleteReason ? restricted('Eliminar', deleteReason) : action('delete-task', 'Eliminar', task.Id, 'text-button danger-text'));
    }
    let catalogs, projects = [], users = [], tasks = [], detail, saveHandler, generation = 0;
    let toastTimer;

    async function api(resource, method = "GET", data = null) {
        const options = { credentials: "same-origin", headers: { "X-Requested-With": "XMLHttpRequest" } };
        let url = "/Servicio/Consultar?recurso=" + encodeURIComponent(resource);
        if (method !== "GET") {
            url = "/Servicio/Enviar"; options.method = "POST";
            options.body = new URLSearchParams({ recurso: resource, metodo: method, datos: JSON.stringify(data || {}), __RequestVerificationToken: csrf });
        }
        const response = await fetch(url, options);
        if (response.status === 401) { location.replace("/Cuenta/Login"); throw new Error("Su sesión terminó."); }
        const result = await response.json().catch(() => ({ mensaje: "No se pudo completar la solicitud. Recargue la página e intente nuevamente." }));
        if (!response.ok) throw new Error(result.mensaje || "No se pudo completar la operación.");
        return result;
    }
    function banner(id, message, kind) {
        const target = $(id); target.replaceChildren(); target.className = 'notice ' + kind;
        const text = document.createElement('span'); text.textContent = message;
        const close = document.createElement('button'); close.type = 'button'; close.textContent = '×'; close.setAttribute('aria-label','Cerrar aviso'); close.onclick = () => { target.hidden = true; };
        target.append(text, close); target.hidden = false;
    }
    function notify(message) {
        clearTimeout(toastTimer); $('#global-error').hidden = true; banner('#toast',message,'success');
        toastTimer = setTimeout(() => { $('#toast').hidden = true; }, 8000);
    }
    function showError(message) { $('#toast').hidden = true; banner('#global-error',message,'error'); }
    function showInfo(message) { $('#toast').hidden = true; banner('#global-error',message,'info'); }
    function heading(title, subtitle, buttons = "") {
        return `<div class="page-heading"><div><p class="eyebrow">ESPACIO DE TRABAJO</p><h1>${escape(title)}</h1><p class="muted">${escape(subtitle)}</p></div><div class="actions">${buttons}</div></div>`;
    }
    function empty(title, message, button = "") { return `<div class="empty"><span class="empty-icon" aria-hidden="true">▤</span><h3>${escape(title)}</h3><p>${escape(message)}</p>${button}</div>`; }
    function option(items, selected, label = "Nombre") { return items.map(x => `<option value="${x.Id}" ${Number(selected) === x.Id ? "selected" : ""}>${escape(x[label])}</option>`).join(""); }
    function field(label, name, value = "", type = "text", max = 150, required = true) {
        return `<div class="field"><label for="f-${name}">${label}</label><input id="f-${name}" name="${name}" type="${type}" value="${escape(value)}" maxlength="${max}" ${required ? "required" : ""} ${type === "password" ? 'autocomplete="new-password"' : ""}/></div>`;
    }
    function select(label, name, items, selected) { return `<div class="field"><label for="f-${name}">${label}</label><select id="f-${name}" name="${name}" required>${option(items, selected)}</select></div>`; }
    function description(value = "") { return `<div class="field"><label for="f-Descripcion">Descripción</label><textarea id="f-Descripcion" name="Descripcion" rows="4" required maxlength="3000">${escape(value)}</textarea></div>`; }
    function openEditor(title, fields, handler) {
        $("#dialog-title").textContent = title; $("#editor-fields").innerHTML = fields; $("#form-error").textContent = "";
        saveHandler = handler; dialog.showModal();
    }
    $("#close-dialog").onclick = () => dialog.close(); $("#cancel-edit").onclick = () => dialog.close();
    form.addEventListener("submit", async event => {
        event.preventDefault(); $("#form-error").textContent = "";
        for (const element of form.elements) {
            if (element.willValidate && (!element.validity.valid || (element.required && !element.value.trim()))) {
                $("#form-error").textContent = "Revise el campo: " + (form.querySelector(`label[for="${element.id}"]`)?.textContent || "datos del formulario"); element.focus(); return;
            }
        }
        const button = $("#save-edit"); button.disabled = true; button.textContent = "Guardando…";
        $("#close-dialog").disabled = true; $("#cancel-edit").disabled = true;
        try {
            const result = await saveHandler(new FormData(form));
            savedOutsideFilter = false;
            lastSaved = { resource: editorResource, id: Number(result.Id), created: editorNew };
            dialog.close(); await load(); notify((result.mensaje || "Cambios guardados.") + (editorNew ? ' La lista se actualizó automáticamente.' : '') + (savedOutsideFilter ? ' El registro no coincide con los filtros activos. Use Limpiar filtros para verlo.' : ''));
        }
        catch (error) { $("#form-error").textContent = error.message; }
        finally { button.disabled = false; button.textContent = "Guardar cambios →"; $("#close-dialog").disabled = false; $("#cancel-edit").disabled = false; }
    });
    dialog.addEventListener("cancel", event => { if ($("#save-edit").disabled) event.preventDefault(); });
    function confirm(message) {
        return new Promise(resolve => {
            const modal = $("#confirm"); $("#confirm-text").textContent = message;
            const end = answer => { modal.close(); resolve(answer); };
            $("#confirm-yes").onclick = () => end(true); $("#confirm-no").onclick = () => end(false);
            modal.oncancel = event => { event.preventDefault(); end(false); }; modal.showModal();
        });
    }

    async function load() {
        const current = ++generation, route = location.hash.slice(1) || "panel", section = route.split("/")[0];
        $("#global-error").hidden = true; content.setAttribute("aria-busy", "true");
        content.innerHTML = '<div class="loading" role="status"><span class="spinner"></span>Cargando su espacio de trabajo…</div>';
        document.querySelectorAll("[data-nav]").forEach(a => { const active = a.dataset.nav === (section === "proyecto" ? "proyectos" : section); a.classList.toggle("selected", active); if (active) a.setAttribute("aria-current", "page"); else a.removeAttribute("aria-current"); });
        try {
            if (!catalogs) catalogs = await api("catalogos");
            let html;
            if (section === "panel") {
                const [indicators, list, taskList] = await Promise.all([api("indicadores"), api("proyectos"), api("tareas")]);
                if (current !== generation) return;
                projects = list; tasks = taskList;
                const total = indicators.Estados.reduce((n, x) => n + x.Cantidad, 0);
                const completed = indicators.Estados.find(x => x.Id === 12).Cantidad;
                html = heading("Panel general", "Una vista del trabajo, sus prioridades y el avance del equipo.", admin ? action("new-project", "+ Nuevo proyecto", "", "primary") : action("new-task", "+ Nueva tarea", "", "primary"));
                html += `<div class="stats"><article><span>Proyectos activos</span><strong>${indicators.ProyectosActivos}</strong><small>Dentro de su alcance</small></article><article><span>Tareas asignadas a usted</span><strong>${indicators.MisTareas}</strong><small>Todos los estados</small></article><article><span>Tareas completadas</span><strong>${completed}<em> / ${total}</em></strong><small>${total ? Math.round(completed * 100 / total) : 0}% del total visible</small></article><article><span>Tareas vencidas</span><strong class="accent">${indicators.TareasVencidas}</strong><small>Pendientes o en progreso · fecha UTC</small></article></div>`;
                html += `<div class="dashboard-grid"><section class="card"><div class="section-title"><h2>Distribución del trabajo</h2><span class="muted">${total} tareas</span></div>${indicators.Estados.map(x => `<div class="chart-row"><div>${pill(x.Nombre, x.Id)}<strong>${x.Cantidad}</strong></div><progress max="${Math.max(total, 1)}" value="${x.Cantidad}" aria-label="${escape(x.Nombre)}: ${x.Cantidad}"></progress></div>`).join("")}</section><section class="card"><div class="section-title"><h2>Proyectos recientes</h2><a href="#proyectos" class="text-link">Ver todos →</a></div>${projects.length ? projects.slice(0, 4).map(p => `<a class="project-line" href="#proyecto/${p.Id}"><div><strong>${escape(p.Nombre)}</strong><small>${escape(p.Responsable)}</small></div>${pill(p.Estado, p.IdEstado)}</a>`).join("") : empty("Todo comienza con un proyecto", admin ? "Cree el primer proyecto e incorpore al equipo." : "Su administrador le asignará a un proyecto.")}</section></div>`;
                const upcoming = tasks.filter(t => t.IdEstado === 10 || t.IdEstado === 11).sort((a, b) => a.FechaVencimiento.localeCompare(b.FechaVencimiento)).slice(0, 5);
                html += `<section class="card spaced"><div class="section-title"><h2>Próximos vencimientos</h2><a class="text-link" href="#tareas">Ver tareas →</a></div>${taskTable(upcoming)}</section>`;
            } else if (section === "proyectos") {
                projects = await api("proyectos"); if (current !== generation) return;
                html = heading("Proyectos", "Organice responsables, integrantes y fechas de cada iniciativa.", admin ? action("new-project", "+ Nuevo proyecto", "", "primary") : "");
                html += `<div class="toolbar"><label class="search-label">Buscar proyecto<input id="filter-search" type="search" placeholder="Nombre o responsable…"/></label><label>Estado<select id="filter-state"><option value="">Todos los estados</option><option value="1">Activo</option><option value="2">Cerrado</option></select></label><span class="muted">${projects.length} proyectos</span></div><div id="project-list" class="project-grid">${projectCards(projects)}</div>`;
            } else if (section === "proyecto" && /^proyecto\/\d+$/.test(route)) {
                const id = Number(route.split("/")[1]);
                [detail, tasks] = await Promise.all([api("proyectos/" + id), api("tareas?proyecto=" + id)]); if (current !== generation) return;
                const p = detail.Proyecto;
                html = '<a class="back-link" href="#proyectos">← Volver a proyectos</a>' + heading(p.Nombre, p.IdEstado === 1 ? "Detalle del proyecto y tareas del equipo." : "Proyecto cerrado · los datos se conservan en modo de consulta.", p.IdEstado === 1 ? (admin ? action("edit-project", "Editar proyecto", p.Id) : "") + action("new-task", "+ Nueva tarea", p.Id, "primary") : "");
                html += `<section class="card"><div class="section-title"><h2>Información del proyecto</h2>${pill(p.IdEstado === 1 ? "Activo" : "Cerrado", p.IdEstado)}</div><p class="description">${escape(p.Descripcion)}</p><div class="detail-grid"><div><small>Inicio</small><strong>${date(p.FechaInicio)}</strong></div><div><small>Fin estimado</small><strong>${date(p.FechaFinEstimada)}</strong></div><div><small>Responsable</small><strong>${escape(detail.Miembros.find(m => m.Id === p.IdResponsable)?.Nombre || "—")}</strong></div></div><h3>Integrantes</h3><div class="chips">${detail.Miembros.map(m => `<span>${escape(m.Nombre)}${m.Activo ? "" : " · Inactivo"}</span>`).join("")}</div></section><section class="card spaced"><div class="section-title"><h2>Tareas del proyecto</h2><span class="muted">${tasks.length} registradas</span></div><div id="task-list">${taskTable(tasks)}</div></section>`;
            } else if (section === "tareas") {
                [tasks, projects] = await Promise.all([api("tareas"), api("proyectos")]); if (current !== generation) return;
                html = heading(admin ? "Tareas" : "Mis tareas", "Consulte asignaciones, estados, prioridades y fechas de vencimiento.", action("new-task", "+ Nueva tarea", "", "primary"));
                html += `<div class="toolbar"><label class="search-label">Buscar tarea<input id="filter-search" type="search" placeholder="Título, proyecto o persona…"/></label><label>Estado<select id="filter-state"><option value="">Todos los estados</option>${option(catalogs.Estados.filter(x => x.EntidadAplicacion === "Tarea"))}</select></label></div><section class="card" id="task-list">${taskTable(tasks)}</section>`;
            } else if (section === "usuarios" && admin) {
                users = await api("usuarios"); if (current !== generation) return;
                html = heading("Usuarios", "Administre colaboradores, roles y acceso al sistema.", action("new-user", "+ Agregar usuario", "", "primary"));
                html += `<div class="toolbar"><label class="search-label">Buscar usuario<input id="filter-search" type="search" placeholder="Nombre o correo…"/></label><span class="muted">${users.filter(u => u.Activo).length} activos · ${users.length} registrados</span></div><section class="card" id="user-list">${userTable(users)}</section>`;
            } else { location.hash = "panel"; return; }
            if (current !== generation) return;
            content.innerHTML = html; $("#breadcrumb").textContent = "Espacio de trabajo / " + (section === "proyecto" ? "Detalle del proyecto" : { panel: "Panel general", proyectos: "Proyectos", tareas: "Tareas", usuarios: "Usuarios" }[section]);
            document.title = "Tecnova · " + $("#breadcrumb").textContent.split(" / ")[1];
            bindFilters(section);
        } catch (error) { if (current === generation) { content.innerHTML = empty("No se pudo cargar esta sección", "Puede intentar nuevamente sin perder los datos guardados.", action("retry", "Reintentar")); showError(error.message); } }
        finally { if (current === generation) content.setAttribute("aria-busy", "false"); }
    }
    function projectCards(list) {
        return list.length ? list.map(p => `<article class="card project-card" data-row-id="${p.Id}"><div class="section-title">${pill(p.Estado, p.IdEstado)}<span class="muted">#${p.Id}</span></div><h2>${escape(p.Nombre)}</h2><p class="clamp">${escape(p.Descripcion)}</p><div class="project-meta"><span>${escape(p.Responsable)}</span><span>Fin: ${date(p.FechaFinEstimada)}</span></div><div class="progress-caption"><span>Avance</span><strong>${p.TotalTareas ? Math.round(p.Completadas * 100 / p.TotalTareas) : 0}%</strong></div><progress max="${Math.max(p.TotalTareas, 1)}" value="${p.Completadas}" aria-label="Tareas completadas"></progress><small class="muted">${p.Completadas} de ${p.TotalTareas} tareas completadas</small><div class="card-actions"><a class="secondary" href="#proyecto/${p.Id}">Ver proyecto →</a>${!admin ? restricted('Editar','Solo un administrador puede editar proyectos.') : p.IdEstado !== 1 ? restricted('Editar','El proyecto está cerrado y es de solo lectura.') : action('edit-project','Editar',p.Id,'text-button')}${!admin ? restricted('Eliminar','Solo un administrador puede eliminar proyectos vacíos.') : p.IdEstado !== 1 ? restricted('Eliminar','No se eliminan proyectos cerrados. Se conserva su historial.') : p.TotalTareas > 0 ? restricted('Eliminar','Este proyecto tiene tareas. Finalice el trabajo y cierre el proyecto para conservar su historial.') : action('delete-project','Eliminar',p.Id,'text-button danger-text')}</div></article>`).join("") : empty("No hay proyectos para mostrar", "Revise los filtros o espere a que se le asigne un proyecto.");
    }
    function taskTable(list) {
        if (!list.length) return empty("Sin tareas en esta vista", "Las tareas que registre aparecerán aquí con su estado y responsable.");
        return `<div class="table-wrap"><table><thead><tr><th>Tarea / Proyecto</th><th>Asignada a</th><th>Estado</th><th>Prioridad</th><th>Vencimiento</th><th>Acciones</th></tr></thead><tbody>${list.map(t => `<tr data-row-id="${t.Id}"><td><strong>${escape(t.Titulo)}</strong><small>${escape(t.Proyecto)}</small></td><td>${escape(t.Asignado)}</td><td>${pill(t.Estado, t.IdEstado)}</td><td><span class="priority p${t.IdPrioridad}">${escape(t.Prioridad)}</span></td><td>${date(t.FechaVencimiento)}${dueLabel(t)}</td><td><div class="table-actions">${taskActions(t)}</div></td></tr>`).join("")}</tbody></table></div>`;
    }
    function userTable(list) {
        return list.length ? `<div class="table-wrap"><table><thead><tr><th>Colaborador</th><th>Rol</th><th>Acceso</th><th>Acciones</th></tr></thead><tbody>${list.map(u => `<tr data-row-id="${u.Id}"><td><strong>${escape(u.Nombre)}</strong><small>${escape(u.Email)}</small></td><td>${escape(u.Rol)}</td><td>${pill(u.Activo ? "Activo" : "Inactivo", u.Activo ? 1 : 2)}</td><td><div class="table-actions">${action("edit-user", "Editar", u.Id, "text-button")}${u.Activo && u.Id !== userId ? action("deactivate-user", "Desactivar", u.Id, "text-button danger-text") : ""}</div></td></tr>`).join("")}</tbody></table></div>` : empty("No hay coincidencias", "Pruebe con otro nombre o correo.");
    }
    function bindFilters(section) {
        renderList = null;
        if (section === 'proyecto') $('.page-heading .actions').insertAdjacentHTML('beforeend',action('history-project','Historial de cambios',detail.Proyecto.Id));
        const kind = section === 'proyecto' ? 'tareas' : section;
        const target = $(kind === 'tareas' ? '#task-list' : kind === 'proyectos' ? '#project-list' : '#user-list');
        if (!target || !['tareas','proyectos','usuarios'].includes(kind)) return;
        const rows = kind === 'tareas' ? tasks : kind === 'proyectos' ? projects : users;
        const key = location.hash || '#panel';
        if (!listStates.has(key)) listStates.set(key,{search:'',state:'',owner:'',priority:'',from:'',to:'',due:'',page:1,size:10});
        const state = listStates.get(key);
        if (lastSaved?.resource === kind && lastSaved.created) Object.assign(state,{search:'',state:'',owner:'',priority:'',from:'',to:'',due:'',page:1});
        let toolbar = $('.toolbar');
        if (!toolbar) {
            toolbar = document.createElement('div'); toolbar.className = 'toolbar';
            toolbar.innerHTML = '<label class="search-label">Buscar tarea<input id="filter-search" type="search" placeholder="Título, proyecto o persona…"/></label><label>Estado<select id="filter-state"><option value="">Todos los estados</option>' + option(catalogs.Estados.filter(x=>x.EntidadAplicacion==='Tarea')) + '</select></label>';
            target.before(toolbar);
        }
        if (kind !== 'usuarios') {
            const people = [...new Map(rows.map(x => [kind==='tareas'?x.IdAsignado:x.IdResponsable,{Id:kind==='tareas'?x.IdAsignado:x.IdResponsable,Nombre:kind==='tareas'?x.Asignado:x.Responsable}])).values()].sort((a,b)=>a.Nombre.localeCompare(b.Nombre));
            toolbar.insertAdjacentHTML('beforeend', '<label>Responsable<select id="filter-owner"><option value="">Todos</option>'+option(people)+'</select></label>' + (kind==='tareas' ? '<label>Prioridad<select id="filter-priority"><option value="">Todas</option>'+option(catalogs.Prioridades)+'</select></label><label>Vencimiento<select id="filter-due"><option value="">Todos</option><option value="overdue">Vencidas</option><option value="today">Vencen hoy</option><option value="soon">Próximos 3 días (incluye hoy)</option></select></label>' : '') + '<label>Desde ('+(kind==='tareas'?'vencimiento':'fin estimado')+')<input id="filter-from" type="date"/></label><label>Hasta<input id="filter-to" type="date"/></label>');
        }
        toolbar.insertAdjacentHTML('beforeend','<button type="button" class="secondary" data-action="clear-filters">Limpiar filtros</button>');
        const pager = document.createElement('div'); pager.className = 'pagination'; target.after(pager);
        if (kind==='tareas') target.insertAdjacentHTML('beforebegin','<p class="permission-help">Los botones con candado explican por qué una acción no está disponible. Avisos de vencimiento según fecha UTC.</p>');
        const controls = ['search','state','owner','priority','from','to','due'];
        renderList = () => {
            const invalid = state.from && state.to && state.from > state.to;
            const filtered = invalid ? [] : lists.filter(rows,state,kind);
            if (lastSaved?.resource === kind) {
                const index = filtered.findIndex(x=>x.Id===lastSaved.id);
                if (index >= 0) state.page=Math.floor(index/state.size)+1;
                else savedOutsideFilter = true;
            }
            const result = lists.paginate(filtered,state.page,state.size); state.page=result.page;
            target.innerHTML = invalid ? empty('Revise el rango de fechas','La fecha desde no puede ser posterior a la fecha hasta.') : kind==='tareas'?taskTable(result.items):kind==='proyectos'?projectCards(result.items):userTable(result.items);
            pager.innerHTML = `<span role="status">${result.from}–${result.to} de ${result.total} resultados</span><label>Por página<select id="page-size">${[5,10,25,50].map(n=>`<option ${n===result.size?'selected':''}>${n}</option>`).join('')}</select></label><div class="pager-controls"><button type="button" class="secondary" data-action="previous-page" ${result.page===1?'disabled':''}>← Anterior</button><span>Página ${result.page} de ${result.pages}</span><button type="button" class="secondary" data-action="next-page" ${result.page===result.pages?'disabled':''}>Siguiente →</button></div>`;
            $('#page-size').onchange = event => { state.size=Number(event.target.value); state.page=1; renderList(); };
            if (lastSaved?.resource === kind) {
                const row = target.querySelector('[data-row-id="'+lastSaved.id+'"]');
                if (row) row.classList.add('recent-row');
                lastSaved=null;
            }
        };
        for (const name of controls) {
            const element = $('#filter-'+name); if (!element) continue;
            element.value=state[name];
            element.addEventListener(name==='search'?'input':'change',()=>{state[name]=element.value;state.page=1;renderList();});
        }
        renderList();
    }
    function historyMarkup(records) {
        if (!records.length) return '<p class="muted">Todavía no hay cambios registrados. Las nuevas modificaciones aparecerán aquí; no se reconstruyen cambios anteriores.</p>';
        return '<p class="hint">Fecha y hora locales de su dispositivo.</p><ol class="history-list">'+records.map(h=>`<li><div class="history-top"><strong>${escape(h.Accion)}${h.IdTarea?' · Tarea #'+h.IdTarea:''}</strong><time datetime="${escape(h.Fecha)}">${escape(new Date(h.Fecha).toLocaleString('es-CR',{dateStyle:'medium',timeStyle:'medium'}))}</time></div><p class="history-actor">${escape(h.Usuario)}</p><details><summary>Ver cambios</summary><p class="history-detail">${escape(h.Detalle)}</p></details></li>`).join('')+'</ol>';
    }
    function readOnlyDialog() {
        $('#save-edit').hidden=true; $('#cancel-edit').textContent='Cerrar';
        dialog.addEventListener('close',()=>{$('#save-edit').hidden=false;$('#cancel-edit').textContent='Cancelar';},{once:true});
    }
    async function projectHistory(id) {
        const records=await api('proyectos/'+id+'/historial');
        openEditor('Historial de cambios',historyMarkup(records),()=>{}); readOnlyDialog();
    }

    async function editUser(id) {
        editorResource="usuarios"; editorNew=!id;
        const u = id ? await api("usuarios/" + id) : { IdRol: 2, Activo: true };
        openEditor(id ? "Editar usuario" : "Agregar usuario", field("Nombre completo", "Nombre", u.Nombre, "text", 120) + field("Correo electrónico", "Email", u.Email, "email", 180) + select("Rol", "IdRol", catalogs.Roles, u.IdRol) + field(id ? "Nueva contraseña (opcional)" : "Contraseña inicial", "Contrasena", "", "password", 128, !id) + '<p class="hint">Mínimo 12 caracteres con mayúscula, minúscula, número y símbolo. Cambiar contraseña, rol o acceso invalida las sesiones previas.</p>' + `<label class="check"><input type="checkbox" name="Activo" ${u.Activo ? "checked" : ""}/> Cuenta activa</label>`, data => api("usuarios" + (id ? "/" + id : ""), id ? "PUT" : "POST", { Nombre: data.get("Nombre"), Email: data.get("Email"), IdRol: Number(data.get("IdRol")), Contrasena: data.get("Contrasena"), Activo: data.has("Activo"), Version: u.Version }));
    }
    async function editProject(id) {
        editorResource="proyectos"; editorNew=!id;
        const allUsers = await api("usuarios");
        const data = id ? await api("proyectos/" + id) : { Proyecto: { IdResponsable: userId, IdEstado: 1 }, Miembros: [] };
        const p = data.Proyecto;
        const members = new Set(data.Miembros.map(m => m.Id));
        openEditor(id ? "Editar proyecto" : "Crear proyecto", field("Nombre del proyecto", "Nombre", p.Nombre) + description(p.Descripcion) + '<div class="form-grid">' + field("Fecha de inicio", "FechaInicio", iso(p.FechaInicio), "date") + field("Fin estimado", "FechaFinEstimada", iso(p.FechaFinEstimada), "date") + '</div>' + select("Responsable", "IdResponsable", allUsers.filter(u => u.Activo && u.IdRol === 1), p.IdResponsable) + (id ? select("Estado", "IdEstado", catalogs.Estados.filter(s => s.EntidadAplicacion === "Proyecto"), p.IdEstado) : '<input type="hidden" name="IdEstado" value="1"/>') + `<fieldset><legend>Integrantes del proyecto</legend><p class="hint">El responsable se incluye automáticamente. Un integrante con tareas asignadas debe conservarse o sus tareas deben reasignarse.</p><div class="member-list">${allUsers.filter(u => u.Activo || members.has(u.Id)).map(u => `<label class="check"><input type="checkbox" name="Miembros" value="${u.Id}" ${members.has(u.Id) ? "checked" : ""}/> ${escape(u.Nombre)} <small>${escape(u.Rol)}${u.Activo ? "" : " · Inactivo"}</small></label>`).join("")}</div></fieldset>`, data => api("proyectos" + (id ? "/" + id : ""), id ? "PUT" : "POST", { Nombre: data.get("Nombre"), Descripcion: data.get("Descripcion"), FechaInicio: data.get("FechaInicio"), FechaFinEstimada: data.get("FechaFinEstimada"), IdResponsable: Number(data.get("IdResponsable")), IdEstado: Number(data.get("IdEstado")), Miembros: data.getAll("Miembros").map(Number), Version: p.Version }));
    }
    async function editTask(id, projectId) {
        editorResource="tareas"; editorNew=!id;
        const t = id ? await api("tareas/" + id) : { IdEstado: 10, IdPrioridad: 2, IdAsignado: userId };
        const available = (await api("proyectos")).filter(p => p.IdEstado === 1);
        if (!available.length) throw new Error("Necesita un proyecto activo en el que participe para crear tareas.");
        const selected = t.IdProyecto || Number(projectId) || available[0].Id;
        const pd = await api("proyectos/" + selected);
        openEditor(id ? "Editar tarea" : "Crear tarea", select("Proyecto", "IdProyecto", available, selected) + field("Título de la tarea", "Titulo", t.Titulo) + description(t.Descripcion) + select("Asignar a", "IdAsignado", pd.Miembros.filter(m => m.Activo), t.IdAsignado) + '<div class="form-grid">' + select("Estado", "IdEstado", catalogs.Estados.filter(s => s.EntidadAplicacion === "Tarea"), t.IdEstado) + select("Prioridad", "IdPrioridad", catalogs.Prioridades, t.IdPrioridad) + '</div>' + field("Fecha de vencimiento", "FechaVencimiento", iso(t.FechaVencimiento), "date") + `<p class="hint" id="date-range">Fechas del proyecto: ${date(pd.Proyecto.FechaInicio)} al ${date(pd.Proyecto.FechaFinEstimada)}.</p>`, data => api("tareas" + (id ? "/" + id : ""), id ? "PUT" : "POST", { IdProyecto: Number($("#f-IdProyecto").value), Titulo: data.get("Titulo"), Descripcion: data.get("Descripcion"), IdAsignado: Number(data.get("IdAsignado")), IdEstado: Number(data.get("IdEstado")), IdPrioridad: Number(data.get("IdPrioridad")), FechaVencimiento: data.get("FechaVencimiento"), Version: t.Version }));
        $("#f-IdProyecto").disabled = !!id;
        $("#f-FechaVencimiento").min = iso(pd.Proyecto.FechaInicio); $("#f-FechaVencimiento").max = iso(pd.Proyecto.FechaFinEstimada);
        let memberRequest = 0;
        $("#f-IdProyecto").addEventListener("change", async event => {
            const revision = ++memberRequest; $("#save-edit").disabled = true;
            try { const d = await api("proyectos/" + event.target.value); if (revision !== memberRequest) return; $("#f-IdAsignado").innerHTML = option(d.Miembros.filter(m => m.Activo), userId); $("#date-range").textContent = `Fechas del proyecto: ${date(d.Proyecto.FechaInicio)} al ${date(d.Proyecto.FechaFinEstimada)}.`; $("#f-FechaVencimiento").min = iso(d.Proyecto.FechaInicio); $("#f-FechaVencimiento").max = iso(d.Proyecto.FechaFinEstimada); $("#save-edit").disabled = false; }
            catch (error) { $("#form-error").textContent = error.message; }
        });
    }
    async function viewTask(id) {
        const [t, history] = await Promise.all([api("tareas/" + id),api("tareas/"+id+"/historial")]);
        openEditor("Detalle de la tarea", `<div class="section-title">${pill(t.Estado, t.IdEstado)}<span class="priority p${t.IdPrioridad}">${escape(t.Prioridad)}</span></div><h3>${escape(t.Titulo)}</h3><p class="description">${escape(t.Descripcion)}</p><div class="detail-grid"><div><small>Proyecto</small><strong>${escape(t.Proyecto)}</strong></div><div><small>Asignada a</small><strong>${escape(t.Asignado)}</strong></div><div><small>Vencimiento</small><strong>${date(t.FechaVencimiento)}</strong></div><div><small>Creada</small><strong>${date(t.FechaCreacion)}</strong></div></div><h3>Historial de cambios</h3>${historyMarkup(history)}`, () => {});
        readOnlyDialog();
    }
    content.addEventListener("click", async event => {
        const button = event.target.closest("[data-action]"); if (!button) return;
        const name = button.dataset.action, id = Number(button.dataset.id);
        if (name === 'explain') { showInfo(button.dataset.reason); return; }
        if (name === 'previous-page' || name === 'next-page' || name === 'clear-filters') {
            const state=listStates.get(location.hash || '#panel'); if (!state || !renderList) return;
            if(name==='clear-filters') { Object.assign(state,{search:'',state:'',owner:'',priority:'',from:'',to:'',due:'',page:1}); for(const field of ['search','state','owner','priority','from','to','due']) if($('#filter-'+field)) $('#filter-'+field).value=''; }
            else state.page += name==='next-page'?1:-1;
            renderList(); return;
        }
        button.disabled = true; $("#global-error").hidden = true;
        try {
            if (name === "retry") await load();
            if (name === "new-user" || name === "edit-user") await editUser(id);
            if (name === "new-project" || name === "edit-project") await editProject(id);
            if (name === "new-task") await editTask(0, id);
            if (name === "edit-task") await editTask(id);
            if (name === "view-task") await viewTask(id);
            if (name === "history-project") await projectHistory(id);
            if (name === "delete-project" || name === "delete-task" || name === "deactivate-user") {
                const resource = name === "delete-project" ? "proyectos" : name === "delete-task" ? "tareas" : "usuarios";
                const record = await api(resource + "/" + id); const version = record.Proyecto?.Version || record.Version;
                const message = name === "deactivate-user" ? "Se desactivará el acceso de este usuario. Sus registros se conservarán. ¿Desea continuar?" : "Se eliminará este registro si cumple las condiciones del sistema. Esta acción no se puede deshacer. ¿Desea continuar?";
                if (await confirm(message)) { const result = await api(resource + "/" + id + "?version=" + version, "DELETE"); await load(); notify(result.mensaje); }
            }
        } catch (error) { showError(error.message); }
        finally { button.disabled = false; }
    });
    $("#logout").addEventListener("click", async event => {
        event.currentTarget.disabled = true;
        try { const response = await fetch("/Cuenta/Salir", { method: "POST", body: new URLSearchParams({ __RequestVerificationToken: csrf }), credentials: "same-origin" }); if (!response.ok) throw new Error("No se pudo cerrar la sesión. Intente nuevamente."); location.replace("/Cuenta/Login"); }
        catch (error) { showError(error.message); $("#logout").disabled = false; }
    });
    window.addEventListener("hashchange", load);
    window.addEventListener("pageshow", event => { if (event.persisted) location.reload(); });
    load();
})();
