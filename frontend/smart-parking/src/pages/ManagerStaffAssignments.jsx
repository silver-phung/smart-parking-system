import RequireRole from "../components/RequireRole.jsx";
import { People } from "./AdminPeople.jsx";

export default function ManagerStaffAssignments() {
  return <RequireRole role="MANAGER"><People assignmentsOnly /></RequireRole>;
}
