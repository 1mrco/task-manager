// TaskFlow - REST API Frontend Integration Script

const API_BASE = window.location.origin + '/api';

// Application State
let currentUser = null;
let authToken = localStorage.getItem('taskflow_token') || null;
let allTasks = [];
let allUsers = [];
let activeFilterStatus = '';
let searchDebounceTimer = null;

// Initialize App on DOM Load
document.addEventListener('DOMContentLoaded', async () => {
    initTabNavigation();
    
    if (authToken) {
        try {
            currentUser = JSON.parse(localStorage.getItem('taskflow_user'));
            updateUserUI();
            await loadTasks();
            if (currentUser && currentUser.role === 'Admin') {
                await loadUsers();
            }
        } catch (e) {
            console.error('Session expired or invalid:', e);
            logout();
        }
    } else {
        // Guest mode or prompt login
        updateUserUI();
        await loadTasks(); // Will load public/guest or demo state
    }
});

// Navigation & Tabs Setup
function initTabNavigation() {
    const tabTasksBtn = document.getElementById('tabTasksBtn');
    const tabUsersBtn = document.getElementById('tabUsersBtn');
    const tasksView = document.getElementById('tasksView');
    const usersView = document.getElementById('usersView');

    tabTasksBtn.addEventListener('click', () => {
        tabTasksBtn.classList.add('active');
        tabUsersBtn.classList.remove('active');
        tasksView.classList.add('active');
        usersView.classList.remove('active');
    });

    tabUsersBtn.addEventListener('click', async () => {
        tabUsersBtn.classList.add('active');
        tabTasksBtn.classList.remove('active');
        usersView.classList.add('active');
        tasksView.classList.remove('active');
        await loadUsers();
    });
}

// Update UI based on User Login State
function updateUserUI() {
    const guestState = document.getElementById('userGuestState');
    const userState = document.getElementById('userLoggedInState');
    const adminTabs = document.querySelectorAll('.admin-only');
    const userAvatar = document.getElementById('userAvatar');
    const userName = document.getElementById('userName');
    const userRoleBadge = document.getElementById('userRoleBadge');

    if (currentUser && authToken) {
        guestState.classList.add('hidden');
        userState.classList.remove('hidden');

        userAvatar.textContent = (currentUser.name || 'U').charAt(0).toUpperCase();
        userName.textContent = currentUser.name;
        userRoleBadge.textContent = currentUser.role === 'Admin' ? '⚡ أدمن' : 'مستخدم';

        if (currentUser.role === 'Admin') {
            adminTabs.forEach(el => el.classList.remove('hidden'));
        } else {
            adminTabs.forEach(el => el.classList.add('hidden'));
        }
    } else {
        guestState.classList.remove('hidden');
        userState.classList.add('hidden');
        adminTabs.forEach(el => el.classList.add('hidden'));
    }
}

// Authentication API Handlers
async function handleLogin(event) {
    event.preventDefault();
    const email = document.getElementById('loginEmail').value.trim();
    const password = document.getElementById('loginPassword').value;

    try {
        const res = await fetch(`${API_BASE}/auth/login`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ email, password })
        });

        const data = await res.json();
        if (!res.ok) {
            throw new Error(data.message || 'فشل تسجيل الدخول');
        }

        authToken = data.token;
        currentUser = data.user;

        localStorage.setItem('taskflow_token', authToken);
        localStorage.setItem('taskflow_user', JSON.stringify(currentUser));

        updateUserUI();
        closeModal('authModal');
        showToast(`مرحباً بعودتك، ${currentUser.name}!`, 'success');

        await loadTasks();
        if (currentUser.role === 'Admin') {
            await loadUsers();
        }
    } catch (err) {
        showToast(err.message, 'error');
    }
}

async function handleRegister(event) {
    event.preventDefault();
    const name = document.getElementById('regName').value.trim();
    const email = document.getElementById('regEmail').value.trim();
    const password = document.getElementById('regPassword').value;

    try {
        const res = await fetch(`${API_BASE}/auth/register`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ name, email, password })
        });

        const data = await res.json();
        if (!res.ok) {
            throw new Error(data.message || 'فشل إنشاء الحساب');
        }

        authToken = data.token;
        currentUser = data.user;

        localStorage.setItem('taskflow_token', authToken);
        localStorage.setItem('taskflow_user', JSON.stringify(currentUser));

        updateUserUI();
        closeModal('authModal');
        showToast('تم إنشاء حسابك بنجاح!', 'success');

        await loadTasks();
    } catch (err) {
        showToast(err.message, 'error');
    }
}

function logout() {
    authToken = null;
    currentUser = null;
    localStorage.removeItem('taskflow_token');
    localStorage.removeItem('taskflow_user');

    updateUserUI();
    showToast('تم تسجيل الخروج بنجاح', 'info');
    loadTasks();
}

// Tasks API & Rendering
async function loadTasks() {
    const tasksGrid = document.getElementById('tasksGrid');
    const emptyState = document.getElementById('emptyTasksState');

    try {
        const headers = authToken ? { 'Authorization': `Bearer ${authToken}` } : {};
        const queryParams = new URLSearchParams();
        if (activeFilterStatus) queryParams.append('status', activeFilterStatus);

        const searchValue = document.getElementById('searchInput').value.trim();
        if (searchValue) queryParams.append('search', searchValue);

        const res = await fetch(`${API_BASE}/tasks?${queryParams.toString()}`, { headers });
        
        if (res.status === 401) {
            // Unauthorized - show empty state or guest info
            allTasks = [];
            renderTasks([]);
            updateStats([]);
            return;
        }

        if (!res.ok) {
            throw new Error('تعذر جلب المهام من الخادم');
        }

        allTasks = await res.json();
        renderTasks(allTasks);
        updateStats(allTasks);
    } catch (err) {
        console.warn('Failed to load tasks:', err);
        renderTasks([]);
        updateStats([]);
    }
}

function renderTasks(tasks) {
    const tasksGrid = document.getElementById('tasksGrid');
    const emptyState = document.getElementById('emptyTasksState');

    if (!tasks || tasks.length === 0) {
        tasksGrid.innerHTML = '';
        emptyState.classList.remove('hidden');
        return;
    }

    emptyState.classList.add('hidden');
    
    tasksGrid.innerHTML = tasks.map(task => {
        const dueDateFormatted = task.dueDate ? new Date(task.dueDate).toLocaleDateString('ar-EG') : 'بدون تاريخ';
        const statusTextMap = {
            'Pending': 'قيد الانتظار',
            'InProgress': 'قيد التنفيذ',
            'Completed': 'مكتملة',
            'Cancelled': 'ملغاة'
        };

        return `
            <div class="task-card status-${task.status}">
                <div>
                    <div class="task-header">
                        <span class="status-badge ${task.status}">${statusTextMap[task.status] || task.status}</span>
                        <div class="task-actions">
                            <button class="btn-icon" title="تعديل المهمة" onclick="editTask(${task.id})">
                                <i class="fa-solid fa-pen-to-square"></i>
                            </button>
                            <button class="btn-icon text-danger" title="حذف المهمة" onclick="deleteTask(${task.id})">
                                <i class="fa-solid fa-trash-can"></i>
                            </button>
                        </div>
                    </div>
                    <h3 class="task-title">${escapeHtml(task.title)}</h3>
                    <p class="task-desc">${escapeHtml(task.description || 'لا يوجد وصف تفصيلي')}</p>
                </div>

                <div class="task-meta">
                    <div class="task-due">
                        <i class="fa-regular fa-calendar-check"></i> ${dueDateFormatted}
                    </div>
                    <div class="status-quick-change">
                        <select onchange="quickUpdateStatus(${task.id}, this.value)" style="padding: 4px 8px; font-size: 0.75rem; border-radius: 6px;">
                            <option value="Pending" ${task.status === 'Pending' ? 'selected' : ''}>Pending</option>
                            <option value="InProgress" ${task.status === 'InProgress' ? 'selected' : ''}>InProgress</option>
                            <option value="Completed" ${task.status === 'Completed' ? 'selected' : ''}>Completed</option>
                            <option value="Cancelled" ${task.status === 'Cancelled' ? 'selected' : ''}>Cancelled</option>
                        </select>
                    </div>
                </div>
            </div>
        `;
    }).join('');
}

function updateStats(tasks) {
    document.getElementById('statTotal').textContent = tasks.length;
    document.getElementById('statPending').textContent = tasks.filter(t => t.status === 'Pending').length;
    document.getElementById('statInProgress').textContent = tasks.filter(t => t.status === 'InProgress').length;
    document.getElementById('statCompleted').textContent = tasks.filter(t => t.status === 'Completed').length;
}

// Filter and Search Actions
function setFilterStatus(status) {
    activeFilterStatus = status;
    document.querySelectorAll('.filter-btn').forEach(btn => btn.classList.remove('active'));
    event.target.classList.add('active');
    loadTasks();
}

function debounceSearch() {
    clearTimeout(searchDebounceTimer);
    searchDebounceTimer = setTimeout(() => {
        loadTasks();
    }, 300);
}

// Create / Edit / Delete Task
function openTaskModal(taskId = null) {
    if (!authToken) {
        showToast('يرجى تسجيل الدخول أولاً لإنشاء أو تعديل المهام', 'info');
        openAuthModal('login');
        return;
    }

    const modal = document.getElementById('taskModal');
    const form = document.getElementById('taskForm');
    const modalTitle = document.getElementById('taskModalTitle');
    const adminGroup = document.getElementById('adminUserSelectGroup');

    form.reset();
    document.getElementById('taskId').value = '';

    if (currentUser && currentUser.role === 'Admin') {
        adminGroup.classList.remove('hidden');
        populateUserDropdown();
    } else {
        adminGroup.classList.add('hidden');
    }

    if (taskId) {
        const task = allTasks.find(t => t.id === taskId);
        if (task) {
            modalTitle.innerHTML = `<i class="fa-solid fa-pen-to-square"></i> تعديل المهمة #${task.id}`;
            document.getElementById('taskId').value = task.id;
            document.getElementById('taskTitle').value = task.title;
            document.getElementById('taskDescription').value = task.description || '';
            document.getElementById('taskStatus').value = task.status;
            if (task.dueDate) {
                document.getElementById('taskDueDate').value = task.dueDate.split('T')[0];
            }
            if (task.userId && currentUser.role === 'Admin') {
                document.getElementById('taskAssignUserId').value = task.userId;
            }
        }
    } else {
        modalTitle.innerHTML = `<i class="fa-solid fa-plus-circle"></i> إضافة مهمة جديدة`;
    }

    modal.classList.remove('hidden');
}

function editTask(id) {
    openTaskModal(id);
}

async function handleSaveTask(event) {
    event.preventDefault();

    const taskId = document.getElementById('taskId').value;
    const title = document.getElementById('taskTitle').value.trim();
    const description = document.getElementById('taskDescription').value.trim();
    const status = document.getElementById('taskStatus').value;
    const dueDateVal = document.getElementById('taskDueDate').value;

    const payload = {
        title,
        description: description || null,
        status,
        dueDate: dueDateVal ? new Date(dueDateVal).toISOString() : null
    };

    if (currentUser && currentUser.role === 'Admin') {
        const assignedUserId = document.getElementById('taskAssignUserId').value;
        if (assignedUserId) {
            payload.userId = parseInt(assignedUserId);
        }
    }

    try {
        const isUpdate = !!taskId;
        const url = isUpdate ? `${API_BASE}/tasks/${taskId}` : `${API_BASE}/tasks`;
        const method = isUpdate ? 'PUT' : 'POST';

        const res = await fetch(url, {
            method,
            headers: {
                'Content-Type': 'application/json',
                'Authorization': `Bearer ${authToken}`
            },
            body: JSON.stringify(payload)
        });

        if (!res.ok) {
            const data = await res.json();
            throw new Error(data.message || 'فشل حفظ المهمة');
        }

        closeModal('taskModal');
        showToast(isUpdate ? 'تم تحديث المهمة بنجاح' : 'تمت إضافة المهمة بنجاح', 'success');
        await loadTasks();
    } catch (err) {
        showToast(err.message, 'error');
    }
}

async function quickUpdateStatus(taskId, newStatus) {
    if (!authToken) {
        showToast('يرجى تسجيل الدخول للقيام بهذه العملية', 'error');
        loadTasks();
        return;
    }

    try {
        const res = await fetch(`${API_BASE}/tasks/${taskId}`, {
            method: 'PUT',
            headers: {
                'Content-Type': 'application/json',
                'Authorization': `Bearer ${authToken}`
            },
            body: JSON.stringify({ status: newStatus })
        });

        if (!res.ok) {
            throw new Error('فشل تعديل حالة المهمة');
        }

        showToast('تم تحديث حالة المهمة بنجاح', 'success');
        await loadTasks();
    } catch (err) {
        showToast(err.message, 'error');
        await loadTasks();
    }
}

async function deleteTask(taskId) {
    if (!confirm('هل أنت تأكد من رغبتك في حذف هذه المهمة؟')) return;

    try {
        const res = await fetch(`${API_BASE}/tasks/${taskId}`, {
            method: 'DELETE',
            headers: { 'Authorization': `Bearer ${authToken}` }
        });

        if (!res.ok) {
            throw new Error('فشل حذف المهمة');
        }

        showToast('تم حذف المهمة بنجاح', 'success');
        await loadTasks();
    } catch (err) {
        showToast(err.message, 'error');
    }
}

// Admin Users Management API
async function loadUsers() {
    if (!currentUser || currentUser.role !== 'Admin') return;

    const tbody = document.getElementById('usersTableBody');

    try {
        const res = await fetch(`${API_BASE}/users`, {
            headers: { 'Authorization': `Bearer ${authToken}` }
        });

        if (!res.ok) throw new Error('تعذر جلب المستخدمين');

        allUsers = await res.json();
        renderUsers(allUsers);
    } catch (err) {
        tbody.innerHTML = `<tr><td colspan="6" style="text-align:center; color: var(--danger);">فشل تحميل بيانات المستخدمين</td></tr>`;
    }
}

function renderUsers(users) {
    const tbody = document.getElementById('usersTableBody');
    if (!users || users.length === 0) {
        tbody.innerHTML = `<tr><td colspan="6" style="text-align:center;">لا يوجد مستخدمون حالياً</td></tr>`;
        return;
    }

    tbody.innerHTML = users.map(user => {
        const regDate = new Date(user.createdAt).toLocaleDateString('ar-EG');
        const roleBadgeClass = user.role === 'Admin' ? 'status-badge InProgress' : 'status-badge Pending';

        return `
            <tr>
                <td>#${user.id}</td>
                <td><strong>${escapeHtml(user.name)}</strong></td>
                <td>${escapeHtml(user.email)}</td>
                <td><span class="${roleBadgeClass}">${user.role}</span></td>
                <td>${regDate}</td>
                <td>
                    <button class="btn-icon" title="تعديل الدور" onclick="toggleUserRole(${user.id}, '${user.role}')">
                        <i class="fa-solid fa-user-shield"></i>
                    </button>
                    <button class="btn-icon text-danger" title="حذف المستخدم" onclick="deleteUser(${user.id})">
                        <i class="fa-solid fa-user-xmark"></i>
                    </button>
                </td>
            </tr>
        `;
    }).join('');
}

function populateUserDropdown() {
    const select = document.getElementById('taskAssignUserId');
    select.innerHTML = '<option value="">تلقائي (للحساب الحالي)</option>' +
        allUsers.map(u => `<option value="${u.id}">${escapeHtml(u.name)} (${u.email})</option>`).join('');
}

async function toggleUserRole(userId, currentRole) {
    const newRole = currentRole === 'Admin' ? 'User' : 'Admin';
    if (!confirm(`هل أنت متأكد من تغيير دور المستخدم إلى ${newRole}؟`)) return;

    try {
        const res = await fetch(`${API_BASE}/users/${userId}`, {
            method: 'PUT',
            headers: {
                'Content-Type': 'application/json',
                'Authorization': `Bearer ${authToken}`
            },
            body: JSON.stringify({ role: newRole })
        });

        if (!res.ok) throw new Error('فشل تحديث دور المستخدم');

        showToast('تم تغيير صلاحية المستخدم بنجاح', 'success');
        await loadUsers();
    } catch (err) {
        showToast(err.message, 'error');
    }
}

async function deleteUser(userId) {
    if (!confirm('هل أنت متأكد من حذف هذا المستخدم؟ سيتم حذف جميع مهامه التابعة له.')) return;

    try {
        const res = await fetch(`${API_BASE}/users/${userId}`, {
            method: 'DELETE',
            headers: { 'Authorization': `Bearer ${authToken}` }
        });

        if (!res.ok) throw new Error('فشل حذف المستخدم');

        showToast('تم حذف المستخدم بنجاح', 'success');
        await loadUsers();
    } catch (err) {
        showToast(err.message, 'error');
    }
}

// Modal Helpers
function openAuthModal(tab = 'login') {
    switchAuthTab(tab);
    document.getElementById('authModal').classList.remove('hidden');
}

function switchAuthTab(tab) {
    const loginTab = document.getElementById('authLoginTab');
    const regTab = document.getElementById('authRegisterTab');
    const loginForm = document.getElementById('loginForm');
    const regForm = document.getElementById('registerForm');

    if (tab === 'login') {
        loginTab.classList.add('active');
        regTab.classList.remove('active');
        loginForm.classList.remove('hidden');
        regForm.classList.add('hidden');
    } else {
        regTab.classList.add('active');
        loginTab.classList.remove('active');
        regForm.classList.remove('hidden');
        loginForm.classList.add('hidden');
    }
}

function closeModal(modalId) {
    document.getElementById(modalId).classList.add('hidden');
}

function fillAdminCredentials() {
    document.getElementById('loginEmail').value = 'admin@taskmanager.com';
    document.getElementById('loginPassword').value = 'Admin123!';
    showToast('تمت تعبئة بيانات الأدمن التجريبية', 'info');
}

// Toast Notifications Helper
function showToast(message, type = 'info') {
    const container = document.getElementById('toastContainer');
    const toast = document.createElement('div');
    toast.className = `toast toast-${type}`;

    const iconMap = {
        'success': 'fa-circle-check',
        'error': 'fa-circle-exclamation',
        'info': 'fa-circle-info'
    };

    toast.innerHTML = `
        <i class="fa-solid ${iconMap[type]}"></i>
        <span>${escapeHtml(message)}</span>
    `;

    container.appendChild(toast);

    setTimeout(() => {
        toast.style.opacity = '0';
        toast.style.transform = 'translateX(-100%)';
        toast.style.transition = 'all 0.3s ease';
        setTimeout(() => toast.remove(), 300);
    }, 4000);
}

// Security Escape HTML
function escapeHtml(str) {
    if (!str) return '';
    return str.replace(/[&<>"']/g, function(m) {
        return {
            '&': '&amp;',
            '<': '&lt;',
            '>': '&gt;',
            '"': '&quot;',
            "'": '&#039;'
        }[m];
    });
}
